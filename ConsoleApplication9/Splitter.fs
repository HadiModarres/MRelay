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

type private MinorSocket(socket: Socket,majorSocket:Socket,socketBufferSize: int,segmentSize: int,socketException: (Socket*Exception)-> unit,readDone:unit->unit,flushCallback: int-> unit) as this=
    let mutable buffer = Array.create socketBufferSize (new Byte())
    let receiveCallback = new AsyncCallback(this.ReceiveCallback)
    let sendCallback= new AsyncCallback(this.SendCallback)
    let mutable totalAvailable = 0
    let mutable neededForSegment = segmentSize
    let mutable cycleCallback =
        fun() -> ()
    let mutable noMoreCyclesCallback = 
        fun(a: ICycle) -> ()


    member this.ReadMore()=
        printfn "reading"
        ignore(majorSocket.BeginReceive(buffer,totalAvailable,neededForSegment,SocketFlags.None,receiveCallback,null))
    member this.ReceiveCallback(result: IAsyncResult)=
        let readCount = majorSocket.EndReceive(result)
        if readCount < 1 then
            //noMoreCyclesCallback(this)
            readDone()
        else 
            totalAvailable <- (totalAvailable+readCount)
            neededForSegment <- (neededForSegment - readCount)
            if neededForSegment = 0 then 
                neededForSegment <- segmentSize
                cycleCallback()
            else
                this.ReadMore()

    member this.SendCallback(result: IAsyncResult)=
        let sent = socket.EndSend(result)
        if sent <> totalAvailable then
            raise (new Exception("couldn't send all data"))
        else
            let t = totalAvailable
            totalAvailable <- 0
            flushCallback(t)
    member this.Flush()=
        if totalAvailable <> 0 then
            ignore(socket.BeginSend(buffer,0,totalAvailable,SocketFlags.None,sendCallback,null))
        else
            flushCallback(0)
    member this.hasMoreRoom()=
        if (socketBufferSize-totalAvailable)< segmentSize then
            printfn "st"
            false
        else
            true
    interface ICycle with
        member this.CycleCallback
            with get() = cycleCallback
            and set(f:unit->unit)= cycleCallback <- f
        member this.Cycle()=
            this.ReadMore()
        member this.NoMoreCyclesCallback
            with set(f: ICycle -> unit) =  noMoreCyclesCallback <- f
    


[<AllowNullLiteral>]
type StreamSplitter(socketManager: ISocketManager,majorSocket: Socket, minorSockets: Socket[],segmentSize: int,minorSocketBufferSize: int) as this =   // major socket receives and splits the stream to send to minor streams
//    let mutable majorSocketBufferSize = minorSockets.GetLength(0)*minorSocketBufferSize
//    let mutable majorSocketBuffer = Array.create majorSocketBufferSize (new Byte())
    
   // let mutable neededForSegment = segmentSize
    let mutable noMoreCyclesCallback = 
        fun(a: ICycle) -> ()
    
    let mutable cycleCallback =
        fun() -> ()


    let mutable dataNeededToCompleteCycle = minorSocketBufferSize * minorSockets.GetLength(0)
    let buffer = Array.create segmentSize (new Byte())
//    let mutable paused = false
    let mutable pendingData = 0UL
    let mutable totalTransferedData = 0UL
    let chain = new CycleManager()
//    let mutable pauseCallback =
//        fun () -> ()
    let mutable flushedCount = 0
    let mutable dataDone = false 
    let lockobj = new obj()
    do 
        this.init()

    member this.init()=
        for i=0 to minorSockets.GetLength(0)-1 do
            let ms = new MinorSocket(minorSockets.[i],majorSocket,minorSocketBufferSize,segmentSize,this.SocketExceptionOccured,this.majorReadDone,this.socketFlushed)
            chain.AddToChain(ms)
        chain.UpdateChain()
        ()
        ignore(chain.Pause(this.segmentsRead))
        
        ()
        
    member this.segmentsRead()=
        printfn "dkg"
        let p = chain.GetAll.[0] :?> MinorSocket
        if p.hasMoreRoom() = true then
            chain.ResumeOneCycle()
        else
            for d in chain.GetAll do
                (d :?> MinorSocket).Flush()

    member this.SocketExceptionOccured(socket: Socket,exc:Exception)=
        printfn "stub"
    member this.majorReadDone()=
        Monitor.Enter lockobj
        dataDone <- true
        for d in chain.GetAll do
             (d :?> MinorSocket).Flush()
        Monitor.Exit lockobj
    member this.socketFlushed(flushedAmount: int)=
        Monitor.Enter lockobj
        flushedCount <- (flushedCount+1)
        totalTransferedData <- totalTransferedData + (uint64)flushedAmount
        dataNeededToCompleteCycle <- (dataNeededToCompleteCycle-flushedAmount)
        if flushedCount = minorSockets.GetLength(0) then
            flushedCount <- 0
            if dataNeededToCompleteCycle = 0 then
                dataNeededToCompleteCycle <- minorSocketBufferSize * minorSockets.GetLength(0)
            if dataDone = true then
                noMoreCyclesCallback(this)
                socketManager.MajorReadDone()
            
            cycleCallback()
        Monitor.Exit lockobj    
       // this.ReadMoreData()
       
//    member this.MinorSend(result: IAsyncResult)=
//        let socket = result.AsyncState :?> Socket
//        try
//            ignore(socket.EndSend(result))
//        with 
//        | _ as e-> socketManager.SocketExceptionOccured(minorSockets,socket,e)
//        totalTransferedData <- totalTransferedData + pendingData
//        pendingData <- 0UL
//        
//        if neededForSegment = 0 then
//            let h = minorStreamQueue.Dequeue()
//            minorStreamQueue.Enqueue(h)
//            neededForSegment <- segmentSize
//        if dataNeededToCompleteCycle = 0 then
//            dataNeededToCompleteCycle <- segmentSize * minorSockets.GetLength(0)
//            cycleCallback()
//        else
//            this.ReadMoreData()

//    member this.MajorRead(result: IAsyncResult)=
//        try
//            let readCount = majorSocket.EndReceive(result)
//            if readCount < 1 then
//                socketManager.MajorReadDone()
//                noMoreCyclesCallback(this)
//                cycleCallback()
//            else
//                pendingData <- (uint64)readCount
//                neededForSegment <- neededForSegment-readCount
//                dataNeededToCompleteCycle <- dataNeededToCompleteCycle - readCount
//                let k = minorStreamQueue.Peek()
//                try
//                    ignore(k.BeginSend(buffer,0,readCount,SocketFlags.None,minorSendCallback,k))
//                with 
//                | _ as e-> socketManager.SocketExceptionOccured(minorSockets,k,e)
//        with 
//        | _ as e-> socketManager.SocketExceptionOccured(minorSockets,majorSocket,e)
//    member this.TotalData 
//        with get() = totalTransferedData


    
//    member this.ReadMoreData() = 
//            try
//                ignore(majorSocket.BeginReceive(buffer,0,neededForSegment,SocketFlags.None,majorReadCallback,null))
//            with 
//            | _ as e-> socketManager.SocketExceptionOccured(minorSockets,majorSocket,e)
//    member this.majorReadDone()=
//        socketManager.MajorReadDone()

    
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
          //  dataNeededToCompleteCycle <- segmentSize * minorSockets.GetLength(0)
            chain.ResumeOneCycle()
        member this.NoMoreCyclesCallback
            with set(f: ICycle -> unit) =  noMoreCyclesCallback <- f
       
    