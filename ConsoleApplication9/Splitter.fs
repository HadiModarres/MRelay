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

//type private MinorSocket(socket: Socket,socketBufferSize: int,segmentSize: int,socketException: (Socket*Exception)-> unit) as this =
//    let mutable neededBytes = segmentSize
//    let mutable totalAvailable = 0
//    let mutable totalSent = 0
//    let buffer = Array.create socketBufferSize (new Byte())
//    //let mutable sendingData = false
//    let callback = new AsyncCallback(this.SendToMinorSocketCallback)
//    member this.Socket
//        with get()= socket
//    
////    member this.SendingData
////        with get()= sendingData
//
//    member this.ConsumeBuffer(buf: byte[])=
//         if (buf = null) || buf.GetLength(0)=0 then
//       //     printfn "no data to consume"
//            (false,null)
//         else 
//            if buf.GetLength(0) < neededBytes then // socket still needs more data to complete the segment
//                Array.blit buf 0 buffer totalAvailable (buf.GetLength(0))
//                totalAvailable <- (totalAvailable + buf.GetLength(0))
//                neededBytes <- (neededBytes - buf.GetLength(0))
//                (false,null)
//            else // segment done
//                Array.blit buf 0 buffer totalAvailable (neededBytes)
//                totalAvailable <- (totalAvailable + neededBytes)
//                let arr = Array.create (buf.GetLength(0)-neededBytes) (new Byte())
//                Array.blit buf neededBytes arr 0 (buf.GetLength(0)-neededBytes)
//                neededBytes <- segmentSize
//                (true,arr)
//        
//            
//   
//    member this.Flush(minorSocketFlushDone:MinorSocket -> unit)=
//        if totalAvailable = 0 then // no data to be sent
//            minorSocketFlushDone(this)
//    //        printfn "no data to send tho this minor socket"
//        else
//            this.SendMore(minorSocketFlushDone)
//        
//
//    member this.SendToMinorSocketCallback(result: IAsyncResult)=
//       
//            try
//                let sentBytes = socket.EndSend(result)
//       
//           
//              //  printfn "sent %i bytes to minor socket" sentBytes
//                let (f:MinorSocket -> unit)=(result.AsyncState) :?> (MinorSocket -> unit)
//                if sentBytes > 0 then
//                    totalSent <- (totalSent + sentBytes)
//                    if totalSent = totalAvailable then // sent all data
//                        this.Reset()
//      
//                        f(this)
//                    else
//                        do this.SendMore(f)
//                else  // failed to send any data
//          //          printfn "failed to send any data to minor socket"
//                    ()
//            with 
//            | :? SocketException as e-> socketException(socket,e)
//            | :? ObjectDisposedException -> ()  
//            
//    member this.SendMore(minorSocketFlushDone:MinorSocket -> unit)=
//            try
//                ignore(socket.BeginSend(buffer,totalSent,totalAvailable-totalSent,SocketFlags.None,callback,minorSocketFlushDone))
//            with 
//            | :? SocketException as e -> socketException(socket,e)
//            | :? ObjectDisposedException -> ()
//    member this.Reset() = 
//        totalAvailable <- 0
//        totalSent <- 0

[<AllowNullLiteral>]
type StreamSplitter(socketManager: ISocketManager,majorSocket: Socket, minorSockets: Socket[],segmentSize: int,minorSocketBufferSize: int) as this =   // major socket receives and splits the stream to send to minor streams
//    let mutable majorSocketBufferSize = minorSockets.GetLength(0)*minorSocketBufferSize
//    let mutable majorSocketBuffer = Array.create majorSocketBufferSize (new Byte())
    let minorStreamQueue = new Generic.Queue<Socket>()
    let majorReadCallback = new AsyncCallback(this.MajorRead)
    let minorSendCallback = new AsyncCallback(this.MinorSend)

    let mutable neededForSegment = segmentSize
    let mutable noMoreCyclesCallback = 
        fun(a: ICycle) -> ()
    
    let mutable cycleCallback =
        fun() -> ()

    let mutable dataNeededToCompleteCycle = segmentSize * minorSockets.GetLength(0)
    let buffer = Array.create segmentSize (new Byte())
//    let mutable paused = false
    let mutable pendingData = 0UL
    let mutable totalTransferedData = 0UL
//    let mutable pauseCallback =
//        fun () -> ()

    do 
        for socket in minorSockets do
             minorStreamQueue.Enqueue(socket)

       // this.ReadMoreData()
       
    member this.MinorSend(result: IAsyncResult)=
        let socket = result.AsyncState :?> Socket
        try
            ignore(socket.EndSend(result))
        with 
        | _ as e-> socketManager.SocketExceptionOccured(minorSockets,socket,e)
        totalTransferedData <- totalTransferedData + pendingData
        pendingData <- 0UL
        
        if neededForSegment = 0 then
            let h = minorStreamQueue.Dequeue()
            minorStreamQueue.Enqueue(h)
            neededForSegment <- segmentSize
        if dataNeededToCompleteCycle = 0 then
            dataNeededToCompleteCycle <- segmentSize * minorSockets.GetLength(0)
            cycleCallback()
        else
            this.ReadMoreData()

    member this.MajorRead(result: IAsyncResult)=
        try
            let readCount = majorSocket.EndReceive(result)
            if readCount < 1 then
                socketManager.MajorReadDone()
                noMoreCyclesCallback(this)
                cycleCallback()
            else
                pendingData <- (uint64)readCount
                neededForSegment <- neededForSegment-readCount
                dataNeededToCompleteCycle <- dataNeededToCompleteCycle - readCount
                let k = minorStreamQueue.Peek()
                try
                    ignore(k.BeginSend(buffer,0,readCount,SocketFlags.None,minorSendCallback,k))
                with 
                | _ as e-> socketManager.SocketExceptionOccured(minorSockets,k,e)
        with 
        | _ as e-> socketManager.SocketExceptionOccured(minorSockets,majorSocket,e)
    member this.TotalData 
        with get() = totalTransferedData


    
    member this.ReadMoreData() = 
            try
                ignore(majorSocket.BeginReceive(buffer,0,neededForSegment,SocketFlags.None,majorReadCallback,null))
            with 
            | _ as e-> socketManager.SocketExceptionOccured(minorSockets,majorSocket,e)
    member this.majorReadDone()=
        socketManager.MajorReadDone()

    
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
            dataNeededToCompleteCycle <- segmentSize * minorSockets.GetLength(0)
            this.ReadMoreData()
        member this.NoMoreCyclesCallback
            with set(f: ICycle -> unit) =  noMoreCyclesCallback <- f
       
    