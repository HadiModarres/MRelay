///   2014 Sadegh Modarres   modarres.zadeh@gmail.com
/// 
///  This library is free software; you can redistribute it and/or
///  modify it under the terms of the GNU Lesser General Public
///  License as published by the Free Software Foundation; either
///  version 2.1 of the License, or (at your option) any later version.
/// 
///  This library is distributed in the hope that it will be useful,
///  but WITHOUT ANY WARRANTY; without even the implied warranty of
///  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
///  Lesser General Public License for more details.
module Merger
open IDataPipe
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open ISocketManager
open ICycle
open System.Collections.Generic
open System.IO
open CycleManager

let mutable allData = 0
let mutable aggregateData = 0


type private MinorSocket(socket: Socket,majorSocket:Socket,socketBufferSize: int,segmentSize: int,socketException: (Socket*Exception)-> unit,readDone:unit->unit) as this=
    let mutable buffer = Array.create socketBufferSize (new Byte())
    let mutable receivedCount = 0
    let mutable sentCount = 0
    let receiveCallback = new AsyncCallback(this.ReceiveCallback)
    let sendCallback= new AsyncCallback(this.SendCallback)
    let mutable cycleCallback =
        fun() -> ()
    let mutable noMoreCyclesCallback = 
        fun(a: ICycle) -> ()
    let lockobj = new obj()

    let mutable dataDone = false
    do
        this.ReadMore()    
        
    member this.SendMore()=
        if receivedCount = sentCount then
            if dataDone = true then
                buffer <- null // is this necessary or just a result of leakophobia
                readDone()
                //cycleCallback()
            else
                if sentCount = socketBufferSize then
                    sentCount <- 0 
                    receivedCount <- 0
                    this.ReadMore()
                Thread.Sleep(10)
                this.SendMore()
        else
            ignore(majorSocket.BeginSend(buffer,sentCount,min (receivedCount-sentCount) (segmentSize-sentCount%segmentSize),SocketFlags.None,sendCallback,null))
                
    member this.ReadMore()=
        ignore(socket.BeginReceive(buffer,receivedCount,(min (socketBufferSize-receivedCount) segmentSize),SocketFlags.None,receiveCallback,null))
    member this.ReceiveCallback(result: IAsyncResult)=
        let count = socket.EndReceive(result)
        if count < 1 then
            dataDone <- true
        else
            receivedCount <- (receivedCount+count)
            if receivedCount <> socketBufferSize then
                
                this.ReadMore()

    member this.SendCallback(result: IAsyncResult)=
        let sent = majorSocket.EndReceive(result)
        sentCount <- (sentCount+sent)
        if sentCount%segmentSize = 0 then // segment complete
            cycleCallback()
        else
            this.SendMore()

    interface ICycle with
        member this.CycleCallback
            with get() = cycleCallback
            and set(f:unit->unit)= cycleCallback <- f
        member this.Cycle()=
            this.SendMore()
        member this.NoMoreCyclesCallback
            with set(f: ICycle -> unit) =  noMoreCyclesCallback <- f
    

[<AllowNullLiteral>]
type StreamMerger(socketManager: ISocketManager,majorSock:Socket,minorSock: Socket[],segmentSize: int,minorSocketBufferSize: int) as this= // this class is responsible for reading data from multiple minor sockets and aggregate the data to send to major socket
    let mutable totalTransferedData = 0UL
    let mutable pendingData = 0UL
    let mutable cycleCallback =
        fun() -> ()
    let mutable noMoreCyclesCallback = 
        fun(a: ICycle) -> ()

    let chain = new CycleManager()
    do
        this.init()    
    
    member this.init()=
        for i=0 to minorSock.GetLength(0)-1 do
            let ms = new MinorSocket(minorSock.[i],majorSock,minorSocketBufferSize,segmentSize,socketManager.SocketExceptionOccured,this.MergerDone)
            chain.AddToChain(ms)
        chain.UpdateChain()
        ignore(chain.Pause(cycleCallback))
    member this.MergerDone()=
        
        socketManager.MinorReadDone(minorSock)
        noMoreCyclesCallback(this)
        cycleCallback()
    interface IDataPipe with
        member x.TotalTransferedData()= 
            let h = totalTransferedData
            totalTransferedData <- 0UL
            h
    interface ICycle with
        member this.CycleCallback
            with get() = cycleCallback
            and set(f:unit->unit)= cycleCallback <- f
        member this.Cycle()=
//            this.ReadFromHeadOfQueue()
            chain.Resume()
        member this.NoMoreCyclesCallback
            with set(f: ICycle -> unit) =  noMoreCyclesCallback <- f
       