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
module SocketStore

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open Splitter
open Merger
open IDataPipe
open ISocketManager
open System.Collections.Generic



let mutable openPipes = 0

type SocketStore(socketStoreClosedCallback: unit -> unit) =
    let mutable connectedSockets = 0
    let mutable majorSocket: Socket = null
    let mutable majorReadStatus = true
    
    let minorSocketSets = new Generic.List<Socket[]>()
    let minorSetReadStatus = new Generic.List<Boolean>()
    let lockobj = new obj()
    let lockobj2 = new obj()
    let lockobj3 = new obj()
  //  let mutable test = 0
    let mutable closed = false
//    let timerCallback = new TimerCallback(x.SendEmptyAck)
//    let ackCallback = new AsyncCallback(x.SendEmptyAckCallback)
    do
        openPipes <- (openPipes + 1)

    let pred(t: Boolean)=
        if t = true then
            false
        else
            true
    member x.ConnectedSockets
            with get() = connectedSockets
    member x.AddMinorSet(size: int)=
        let newset = Array.create size null
        connectedSockets <- 0
        ignore(minorSocketSets.Add(newset))
        ignore(minorSetReadStatus.Add(true))
    
    member x.GetLastMinorSet()=
        minorSocketSets.[minorSocketSets.Count-1] 
    member x.GetMinorSetAt(index: int)=
        minorSocketSets.[index] 

    member x.AddMinorSocket(socket: Socket)=
        Monitor.Enter lockobj2
        let ar = minorSocketSets.[minorSocketSets.Count-1] 
        ar.[connectedSockets] <- socket
        let con = connectedSockets
        connectedSockets <- (connectedSockets + 1)
        Monitor.Exit lockobj2
        con

    member x.AddMinorSocket(socket: Socket, index: int)=
        Monitor.Enter lockobj2
        let ar = minorSocketSets.[minorSocketSets.Count-1] 
        ar.[index] <- socket
        connectedSockets <- (connectedSockets + 1)
        Monitor.Exit lockobj2

    member x.MajorSocket 
        with get() = majorSocket
        and set y = majorSocket <- y ; 
    
    
//    member private x.MajorStatusCheck(chances: obj)=
//        Monitor.Enter lockobj3
//        if closed = false then 
//      //      printfn "checking"
//          //  let mutable blockingState = majorSocket.Blocking
//            try
//        
//            
//                let tmp = Array.create 1 (new Byte())
//                majorSocket.Blocking <- false
//    //            majorSocket.Send(tmp, 0, 0)
//                ignore(majorSocket.Send(tmp,0,SocketFlags.None))
//                //Console.WriteLine("Connected!")
//            with
//            | :? ObjectDisposedException as e -> x.Close()
//            | _ as e->  printfn "major socket check exception: %A" e.Message
//        
//        
//            printfn "majorsocket connected value: %A" majorSocket.Connected
//            match majorSocket.Connected with
//            | false when (chances:?>int)-1=0 -> printfn "closing, no chances left" ;x.Close()
//            | false when (chances:?>int)-1<>0 -> printfn "chances left: %i" ((chances:?>int)-1);ignore(new Threading.Timer(timerCallback,((chances:?>int)-1),3000,Timeout.Infinite))
//            | true -> ignore(new Threading.Timer(timerCallback,(chances:?>int),3000,Timeout.Infinite))
//            | _ -> x.Close()
//        Monitor.Exit lockobj3
//    
    
//    member x.SendEmptyAck(timerObj: obj)=
//        let tmp = Array.create 1 (new Byte())
//        try
//            printfn "sending ack"
//            ignore(majorSocket.BeginSend(tmp,0,0,SocketFlags.None,ackCallback,null))
//        with
//        | _ -> printfn "empty ack failed" ; x.Close()
//    member x.SendEmptyAckCallback(result: IAsyncResult)=
//        try
//            printfn "ack callback"
//            ignore(majorSocket.EndSend(result))
//            ignore(new Threading.Timer(timerCallback,1,3000,Timeout.Infinite))
//        with
//        | _ -> printfn "empty ack failed" ; x.Close()
//    member private x.SyncMajorReadDone() =
//        Monitor.Enter lockobj
//     //   printfn "major read done"
//        if majorReadStatus = true then
//            majorReadStatus <- false
//
//            try
//                for mset in minorSocketSets.ToArray() do
//                    for sock in mset do
//                        sock.Shutdown(SocketShutdown.Send)
//
//            with 
//            | :? SocketException -> x.Close()
//            | :? ObjectDisposedException -> ()
//            if minorSetReadStatus.Count >0 then
//                for i=minorSetReadStatus.Count-1 downto 0 do
//                    let b = minorSetReadStatus.[i] 
//                    if b = false then
//                        x.Close(minorSocketSets.[i])
////        x.MajorStatusCheck(3) 
//            else
//                x.Close()             
//        Monitor.Exit lockobj
//    member private x.SyncMinorReadDone(set: Socket[])=
//        Monitor.Enter lockobj
//      //  printfn "minor read done"
//        let index = minorSocketSets.IndexOf(set)
//        minorSetReadStatus.[index] <- false
//        try
//            let p = new Predicate<Boolean>(pred)
//            if  Array.TrueForAll<Boolean>(minorSetReadStatus.ToArray(),p) then
//                majorSocket.Shutdown(SocketShutdown.Send)
//            for sock in set do
//                sock.Shutdown(SocketShutdown.Receive)
//        with 
//        | :? SocketException -> x.Close()
//        | :? ObjectDisposedException -> ()
//        if majorReadStatus = false then
//            x.Close(set)
//        Monitor.Exit lockobj
    
    member x.Close(set: Socket[])=
        Monitor.Enter lockobj
       

        let index = minorSocketSets.IndexOf(set)
        if index <> -1 then
            for sock in set do
                    sock.Close()
            minorSetReadStatus.RemoveAt(index)
            minorSocketSets.RemoveAt(index)
            if majorReadStatus = false && minorSocketSets.Count = 0 then
                x.Close()
        Monitor.Exit lockobj
        
    member x.Close()= 
        Monitor.Enter lockobj
        if closed = false then 
            openPipes <- (openPipes-1)
           // printfn "closing pipe!: %i" openPipes
            for mset in minorSocketSets.ToArray() do
                    for sock in mset do
                        if sock <> null then
                            try
                                sock.Shutdown(SocketShutdown.Both)
                            with
                            |_ -> ()
                            sock.Close()    
            minorSetReadStatus.Clear()
            minorSocketSets.Clear()
            if majorSocket <> null then
                try
                    majorSocket.Shutdown(SocketShutdown.Both)
                with 
                | _ -> ()
                majorSocket.Close()
            closed <- true
            socketStoreClosedCallback()

        Monitor.Exit lockobj
        
   
    
    interface ISocketManager with
        member x.MajorReadDone() =
//            if closed = false then
//                x.SyncMajorReadDone() 
            ()
        member x.MinorReadDone(set: Socket[]) =
//            if closed = false then
//                x.SyncMinorReadDone(set)
            ()
        member x.SocketExceptionOccured(socket: Socket,exc: Exception) =
      //      printfn "socket exception: %A %A" sock exc.Message 
      //      x.Close() // should the whole pipe be closed with every socket exception?           
            x.Close()
        member x.SocketExceptionOccured(set: Socket[],socket:Socket,exc: Exception)=
            x.Close()