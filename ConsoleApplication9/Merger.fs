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
open SocketStore
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections


let mutable allData = 0
let mutable aggregateData = 0


type private MinorSocket(socket: Socket,socketBufferSize: int,segmentSize: int,socketException: unit-> unit) as this=
    let buffer = Array.create socketBufferSize (new Byte())
    let  receivedData = ArrayList.Synchronized(new ArrayList(3000));
    let callback = new AsyncCallback(this.ReceiveToMinorSocketCallback)
    let mutable dataDone = false   

    member this.IsDataDone() = 
        dataDone
    member this.GetMoreData() = // return null if no data available
        if (receivedData.Count < 1) then
            null
        else
            let da:byte[] = (receivedData.[0]) :?> byte[]
            receivedData.RemoveAt(0)
            da
   
    member this.TooMuchData(data: byte[])=
            receivedData.Insert(0,data)

    member this.StartReading()=
            try
                ignore(socket.BeginReceive(buffer,0,buffer.GetLength(0),SocketFlags.None,callback,null))
            with 
            | :? SocketException -> socketException()
            | :? ObjectDisposedException -> ()  
    member this.ReceiveToMinorSocketCallback(result: IAsyncResult) = 
            try
                let receivedCount = socket.EndReceive(result) 

                if receivedCount < 1 then
                    dataDone <- true
              
                else
                    let arr = Array.create receivedCount (new Byte())
                    Array.blit buffer 0 arr 0 receivedCount
                    ignore(receivedData.Add(arr))
                    this.StartReading()
            with 
            | :? SocketException -> socketException()
            | :? ObjectDisposedException -> ()  


type MajorSocket(socket: Socket,socketBufferSize: int,segmentSize: int,socketException: unit->unit) as this=
    let mutable  totalAvailable = 0
    let mutable totalSent = 0 
    let buffer = Array.create socketBufferSize (new Byte())
    let mutable neededBytes = segmentSize
    let callback = new AsyncCallback(this.SendToMajorSocketCallback)

    

    member this.Flush(flushDone: unit-> unit) =
        if totalAvailable = 0 then // no data to be sent
            ()//do flushDone()
        else
           
            do this.SendMore(flushDone)

    member this.HaveMoreRoom() =
        if socketBufferSize-totalAvailable < segmentSize then
            false
        else
            true        

    member this.ConsumeBuffer(buff: byte[]) = 
        totalSent <- 0
        
        if (buff = null) || buff.GetLength(0)=0 then
            false,null
        else
            if buff.GetLength(0) < neededBytes then
                Array.blit buff 0 buffer totalAvailable (buff.GetLength(0))
                neededBytes <- (neededBytes - buff.GetLength(0))
                totalAvailable <- (totalAvailable + buff.GetLength(0))
                false,null
            else 
                Array.blit buff 0 buffer totalAvailable neededBytes
                totalAvailable <- (totalAvailable + neededBytes)
                let arr = Array.create ((buff.GetLength(0))-neededBytes) (new Byte())
                Array.blit buff neededBytes arr 0 (arr.GetLength(0)) 
                neededBytes <- segmentSize
                true,arr

            
        
    member private this.SendMore(flushDone:unit -> unit) =
            try
                ignore(socket.BeginSend(buffer,totalSent,totalAvailable-totalSent,SocketFlags.None,callback,flushDone))
            with 
            | :? SocketException -> socketException()
            | :? ObjectDisposedException -> ()
        
    member private this.SendToMajorSocketCallback(result: IAsyncResult)=
            try
                let sentBytes = socket.EndSend(result)
                let (f:unit -> unit)=(result.AsyncState) :?> (unit -> unit)
                if sentBytes > 0 then
                    totalSent <- (totalSent + sentBytes)
                    if totalSent = totalAvailable then // sent all data
                
                        do this.Reset()
                        do f()
                    else
                        this.SendMore(f) // send the rest
                else  // failed to send any data
              //      printfn "couldn't send any data"
                    ()
            with 
            | :? SocketException -> socketException()
            | :? ObjectDisposedException -> ()  


    member private this.Reset()=
        totalAvailable <- 0
        totalSent <- 0

type StreamMerger(socketStore:SocketStore,segmentSize: int,minorSocketBufferSize: int) as this= // this class is responsible for reading data from multiple minor sockets and aggregate the data to send to major socket
    let majorSocketBufferSize = (socketStore.MinorSockets.GetLength(0))*minorSocketBufferSize
    let majorSocketBuffer = Array.create majorSocketBufferSize (new Byte())
    let minorStreamQueue = new Generic.Queue<MinorSocket>()
    let majorSocket = new MajorSocket(socketStore.MajorSocket,majorSocketBufferSize,segmentSize,this.SocketExceptionOccured)
    let mutable feeding = false
    let lockobj = new obj()
    let lockobj2 = new obj()
    let timerCallback = new TimerCallback(this.feedDriver)
    let timer = new Threading.Timer(timerCallback)
    let mutable dataOver = false
    do
        for sock in socketStore.MinorSockets do
            let min = new MinorSocket(sock,minorSocketBufferSize,segmentSize,this.SocketExceptionOccured)
            minorStreamQueue.Enqueue(min)
        

        for sock in minorStreamQueue.ToArray() do
            sock.StartReading();
        ignore(timer.Change(0,Timeout.Infinite))
        
        


    member this.SocketExceptionOccured() = 
        socketStore.Close()
    member this.MajorSocketFlushDone() =              
        feeding <- false
        ignore(timer.Change(0,Timeout.Infinite))
        
    member this.feedDriver(timerObj: obj) =
        if feeding = false then
//            let mutable ass =false
            while this.FeedMajorSocket() do
                feeding <- true
            if feeding then
//                feeding <- true
                ignore(timer.Change(Timeout.Infinite,Timeout.Infinite))
                majorSocket.Flush(this.MajorSocketFlushDone)
            else
                if dataOver = false then
                    ignore(timer.Change(100,Timeout.Infinite))

        
    member this.FeedMajorSocket():bool =
        if majorSocket.HaveMoreRoom() then     
                let sock = minorStreamQueue.Peek()
                let b = sock.GetMoreData();
                
                if (b <> null) then 
                
                    let h = majorSocket.ConsumeBuffer(b)
                
                    let enoughDataForSegment = fst(h)
                    let remainder = snd(h)

                    if enoughDataForSegment then
                        if (remainder <> null) && (remainder.GetLength(0)>0) then
                            sock.TooMuchData(remainder)
                        let s = minorStreamQueue.Dequeue()
                        minorStreamQueue.Enqueue(s)
                    true        
                else 
                    if sock.IsDataDone() && (feeding = false) then
                        dataOver <- true
                        socketStore.MinorReadDone()
                 //       printfn "data done and feeding false"
                    false
        else
            false
            
