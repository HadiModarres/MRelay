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
open SocketStore


type private MinorSocket(socket: Socket,socketBufferSize: int,segmentSize: int,socketException: unit-> unit) as this =
    let mutable neededBytes = segmentSize
    let mutable totalAvailable = 0
    let mutable totalSent = 0
    let buffer = Array.create socketBufferSize (new Byte())
    //let mutable sendingData = false
    let callback = new AsyncCallback(this.SendToMinorSocketCallback)
    member this.Socket
        with get()= socket
    
//    member this.SendingData
//        with get()= sendingData

    member this.ConsumeBuffer(buf: byte[])=
         if (buf = null) || buf.GetLength(0)=0 then
       //     printfn "no data to consume"
            (false,null)
         else 
            if buf.GetLength(0) < neededBytes then // socket still needs more data to complete the segment
                Array.blit buf 0 buffer totalAvailable (buf.GetLength(0))
                totalAvailable <- (totalAvailable + buf.GetLength(0))
                neededBytes <- (neededBytes - buf.GetLength(0))
                (false,null)
            else // segment done
                Array.blit buf 0 buffer totalAvailable (neededBytes)
                totalAvailable <- (totalAvailable + neededBytes)
                let arr = Array.create (buf.GetLength(0)-neededBytes) (new Byte())
                Array.blit buf neededBytes arr 0 (buf.GetLength(0)-neededBytes)
                neededBytes <- segmentSize
                (true,arr)
        
            
   
    member this.Flush(minorSocketFlushDone:MinorSocket -> unit)=
        if totalAvailable = 0 then // no data to be sent
            minorSocketFlushDone(this)
    //        printfn "no data to send tho this minor socket"
        else
            this.SendMore(minorSocketFlushDone)
        

    member this.SendToMinorSocketCallback(result: IAsyncResult)=
       
            try
                let sentBytes = socket.EndSend(result)
       
           
              //  printfn "sent %i bytes to minor socket" sentBytes
                let (f:MinorSocket -> unit)=(result.AsyncState) :?> (MinorSocket -> unit)
                if sentBytes > 0 then
                    totalSent <- (totalSent + sentBytes)
                    if totalSent = totalAvailable then // sent all data
                        this.Reset()
      
                        f(this)
                    else
                        do this.SendMore(f)
                else  // failed to send any data
          //          printfn "failed to send any data to minor socket"
                    ()
            with 
            | :? SocketException -> socketException()
            | :? ObjectDisposedException -> ()  
            
    member this.SendMore(minorSocketFlushDone:MinorSocket -> unit)=
            try
                ignore(socket.BeginSend(buffer,totalSent,totalAvailable-totalSent,SocketFlags.None,callback,minorSocketFlushDone))
            with 
            | :? SocketException -> socketException()
            | :? ObjectDisposedException -> ()
    member this.Reset() = 
        totalAvailable <- 0
        totalSent <- 0


type StreamSplitter(socketStore: SocketStore,segmentSize: int,minorSocketBufferSize: int) as this =   // major socket receives and splits the stream to send to minor streams
    let majorSocketBufferSize = socketStore.MinorSockets.GetLength(0)*minorSocketBufferSize
    let majorSocketBuffer = Array.create majorSocketBufferSize (new Byte())
    let minorStreamQueue = new Generic.Queue<MinorSocket>()
    let callback1 = new AsyncCallback(this.ReceiveToMajorSocketCallback)
    let mutable sendingSocketCount = 0 
    let flushDoneLockObj = new obj()

    do 
        for socket in socketStore.MinorSockets do
             minorStreamQueue.Enqueue(new MinorSocket(socket,minorSocketBufferSize,segmentSize,this.SocketExceptionOccured))

        this.ReadMoreData()
       
            
    

    member this.MinorSocketFlushDone(minorSocket: obj) =
            Monitor.Enter flushDoneLockObj        
            sendingSocketCount <- (sendingSocketCount - 1)
            if sendingSocketCount = 0 then
                this.ReadMoreData()
            Monitor.Exit flushDoneLockObj

    member this.ReceiveToMajorSocketCallback(result: IAsyncResult) =
            try
                let readCount = socketStore.MajorSocket.EndReceive(result)
                if readCount < 1 then
                    this.majorReadDone()
                else
                    let buff = Array.create readCount (new Byte())
                    Array.blit majorSocketBuffer 0 buff 0 readCount
                    this.SplitData buff
                    sendingSocketCount <- socketStore.MinorSockets.GetLength(0)
                    for sock in minorStreamQueue.ToArray() do
                        sock.Flush(this.MinorSocketFlushDone)
            with 
            | :? SocketException -> this.SocketExceptionOccured()
            | :? ObjectDisposedException -> ()  

    
   
        
    member this.SplitData(buffer: byte[]) =
        if (buffer.GetLength(0) > 0) && (buffer <> null) then
            let head = minorStreamQueue.Peek()
            let h = head.ConsumeBuffer(buffer)
            let result = fst(h)
            let remainingBuffer = snd(h)
            if result=true then
                let soc = minorStreamQueue.Dequeue()
                minorStreamQueue.Enqueue(soc)
                this.SplitData(remainingBuffer)

    member this.SocketExceptionOccured() =
           socketStore.Close()
    member this.ReadMoreData() = 
     //       printfn "reading more data"
            try
                ignore(socketStore.MajorSocket.BeginReceive(majorSocketBuffer,0,majorSocketBufferSize,SocketFlags.None,callback1,null))
            with 
            | :? SocketException -> this.SocketExceptionOccured()
            | :? ObjectDisposedException -> ()
    member this.majorReadDone()=
        socketStore.MajorReadDone()