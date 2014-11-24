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
    let mutable buffer = Array.create socketBufferSize 0uy
    let mutable receivedCount = 0
    let mutable sentCount = 0
    let receiveCallback = new AsyncCallback(this.ReceiveCallback)
    let sendCallback= new AsyncCallback(this.SendCallback)
    let mutable cycleCallback =
        fun() -> ()
    let mutable noMoreCyclesCallback = 
        fun(a: ICycle) -> ()
    let lockobj = new obj()
    let lockobj2 = new obj()
    let mutable sendWait = false

    let mutable dataDone = false
    let lockobj = new obj()
    do
        this.ReadMore()    
        
    member this.SendMore()=
        Monitor.Enter lockobj
        
        if receivedCount = sentCount then
            if dataDone = true then
                buffer <- null // is this necessary or just a result of leakophobia
             //   Array.Resize<byte>(&buffer,0)
                readDone()
                //cycleCallback()
            else
                sendWait <- true

                if sentCount = socketBufferSize then
                    sentCount <- 0 
                    receivedCount <- 0
                    this.ReadMore()
                //this.SendMore()
        else
            try
                ignore(majorSocket.BeginSend(buffer,sentCount,min (receivedCount-sentCount) (segmentSize-sentCount%segmentSize),SocketFlags.None,sendCallback,null))
            with
            | _ as e -> socketException(majorSocket,e) 
        Monitor.Exit lockobj
    member this.ReadMore()=
        try
            ignore(socket.BeginReceive(buffer,receivedCount,(min (socketBufferSize-receivedCount) segmentSize),SocketFlags.None,receiveCallback,null))
        with
        | _ as e -> socketException(socket,e)

    member this.ReceiveCallback(result: IAsyncResult)=
        Monitor.Enter lockobj
        try
            let count = socket.EndReceive(result)
            if count < 1 then
                dataDone <- true
                if sendWait = true then
                    sendWait <- false
                    this.SendMore()
           //   readDone()
            //    Monitor.Exit lockobj
            else
                receivedCount <- (receivedCount+count)
                if receivedCount <> socketBufferSize then
                    this.ReadMore()
            //    Monitor.Exit lockobj
                if sendWait = true then
                    sendWait <- false
                    this.SendMore()
        with
        | _ as e -> socketException(socket,e)
        Monitor.Exit lockobj
    member this.SendCallback(result: IAsyncResult)=
        try
            let sent = majorSocket.EndReceive(result)
            sentCount <- (sentCount+sent)
            if sentCount%segmentSize = 0 then // segment complete
                cycleCallback()
            else
                this.SendMore()
        with
        | _ as e -> socketException(majorSocket,e)
    member this.finali()=
        buffer <- null
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
   // let mutable pendingData = 0UL
    let mutable cycleCallback =
        fun() -> ()
    let mutable noMoreCyclesCallback = 
        fun(a: ICycle) -> ()
    let cycleSize = minorSocketBufferSize*minorSock.GetLength(0)
    let chain = new CycleManager()
    do
        this.init()    
    
    member this.init()=
        for i=0 to minorSock.GetLength(0)-1 do
            let ms = new MinorSocket(minorSock.[i],majorSock,minorSocketBufferSize,segmentSize,socketManager.SocketExceptionOccured,this.MergerDone)
            chain.AddToChain(ms)
        chain.UpdateChain()
        ignore(chain.Pause(this.segmentsSent))

    member this.segmentsSent()=
        totalTransferedData <- totalTransferedData + (uint64)(segmentSize*minorSock.GetLength(0))
        if (totalTransferedData%(uint64)cycleSize = 0UL) then
            cycleCallback()
        else
            chain.ResumeOneCycle()
    member this.MergerDone()=
        
        socketManager.MinorReadDone(minorSock)
        noMoreCyclesCallback(this)
        cycleCallback()
    interface IDataPipe with
        member x.TotalTransferedData()= 
            totalTransferedData
    interface ICycle with
        member this.CycleCallback
            with get() = cycleCallback
            and set(f:unit->unit)= cycleCallback <- f
        member this.Cycle()=
//            this.ReadFromHeadOfQueue()
            chain.ResumeOneCycle()
        member this.NoMoreCyclesCallback
            with set(f: ICycle -> unit) =  noMoreCyclesCallback <- f
       