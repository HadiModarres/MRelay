// Copyright 2014 Hadi Modarres
// modarres.zadeh@gmail.com
//
// This file is part of MRelay.
// MRelay is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MRelay is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
module Splitter

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open IDataPipe
open ISocketManager
open ICycle
open CycleManager

type private MinorSocket(socket: Socket,segmentSize: int,socketBufferSize: int,socketException: (Socket*Exception)-> unit,flushCallback: unit-> unit) as this=
    let mutable buffer = Array.create socketBufferSize (0uy)
    let sendCallback= new AsyncCallback(this.SendCallback)
    let mutable totalFilled = 0
    let mutable neededForSegment = segmentSize
    
    member this.SendCallback(result: IAsyncResult)=
        try
            ignore(socket.EndSend(result))
        with
        | _ as e-> socketException(socket,e)
        totalFilled <- 0 
        flushCallback()

    member this.ConsumeBuffer(buf: byte[],totalConsumed: int,totalAvailable: int)=
        let totalConsumable = totalAvailable-totalConsumed
        if totalConsumable < neededForSegment then
            Array.blit buf totalConsumed buffer totalFilled totalConsumable
            totalFilled <- totalFilled+totalConsumable
            neededForSegment <- neededForSegment-totalConsumable
            (false,totalAvailable)
        else
            Array.blit buf totalConsumed buffer totalFilled neededForSegment
            totalFilled <- totalFilled+neededForSegment
            let tm = neededForSegment
            neededForSegment <- segmentSize
            (true,totalConsumed+tm)
    
    member this.Flush()=
        try
            ignore(socket.BeginSend(buffer,0,totalFilled,SocketFlags.None,sendCallback,null))
        with
        | _ as e-> socketException(socket,e)

[<AllowNullLiteral>]
type StreamSplitter(socketManager: ISocketManager,majorSocket: Socket, minorSockets: Socket[],segmentSize: int,minorSocketBufferSize: int) as this =   // major socket receives and splits the stream to send to minor streams
    let mutable buffer = Array.create (minorSocketBufferSize*minorSockets.GetLength(0)) 0uy
    let mutable noMoreCyclesCallback = 
        fun(a: ICycle) -> ()
    
    let mutable cycleCallback =
        fun() -> ()

    let minorStreamQueue = Generic.Queue<MinorSocket>()

    let lockobj = new obj()
    let mutable dataNeededToCompleteCycle = minorSocketBufferSize * minorSockets.GetLength(0)

    let mutable pendingData = 0 
    let mutable totalData = 0UL

    
    let mutable totalAvailable = 0
    let mutable totalConsumed = 0

    let mutable flushedCount = 0
    let receiveCallback = new AsyncCallback(this.ReceiveCallback)
    //let mutable dataDone = false
    
    do
        for sock in minorSockets do
            minorStreamQueue.Enqueue(new MinorSocket(sock,segmentSize,minorSocketBufferSize,socketManager.SocketExceptionOccured,this.socketFlushed))

    member this.ReceiveCallback(result: IAsyncResult)=
        try
            let readCount = majorSocket.EndReceive(result)
            if readCount < 1 then
                buffer <- null
                minorStreamQueue.Clear()
                socketManager.MajorReadDone()
                noMoreCyclesCallback(this)
                cycleCallback()
            else
                pendingData <- readCount
                dataNeededToCompleteCycle <- dataNeededToCompleteCycle-readCount
                totalAvailable <- readCount
                totalConsumed <- 0
                this.DistributeData()
                this.FlushAll()
        with
        | _ as e-> socketManager.SocketExceptionOccured(majorSocket,e)
    member this.ReadMore()=
        try
            ignore(majorSocket.BeginReceive(buffer,0,dataNeededToCompleteCycle,SocketFlags.None,receiveCallback,null))
        with
        | _ as e-> socketManager.SocketExceptionOccured(majorSocket,e)
    member this.DistributeData()=
        let p = minorStreamQueue.Peek()
        let h = p.ConsumeBuffer(buffer,totalConsumed,totalAvailable)
        let isSegmentCompleted = fst(h)
        totalConsumed <- snd(h)
        if isSegmentCompleted = true then
            let q = minorStreamQueue.Dequeue()
            minorStreamQueue.Enqueue(q)
        if totalConsumed <> totalAvailable then
            this.DistributeData()

    member this.FlushAll()=
        for ms in minorStreamQueue.ToArray() do
            ms.Flush()
    
    member this.socketFlushed()=
        Monitor.Enter lockobj
        flushedCount <- flushedCount+1
        if flushedCount = minorSockets.GetLength(0) then
            totalData <- totalData+(uint64) pendingData
            pendingData <- 0
            flushedCount <- 0
            if dataNeededToCompleteCycle = 0 then
                dataNeededToCompleteCycle <- minorSocketBufferSize*minorSockets.GetLength(0) 
                
                cycleCallback()         
            else
                this.ReadMore()
        Monitor.Exit lockobj
    
    interface IDataPipe with
        member x.TotalTransferedData()= 
            totalData          
    interface ICycle with
        member this.CycleCallback
            with get() = cycleCallback
            and set(f:unit->unit)= cycleCallback <- f
        member this.Cycle()=
            this.ReadMore()
            
        member this.NoMoreCyclesCallback
            with set(f: ICycle -> unit) =  noMoreCyclesCallback <- f
       
    