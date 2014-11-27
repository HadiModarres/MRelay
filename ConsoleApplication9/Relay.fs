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
module Relay

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open RelayMonitor
open IDataPipe
open Pipe
open IPipeManager
open IMonitorDelegate
open System.Collections.Generic

let guidSize = 16 // size of guid in bytes
let mutable totalAcceptedConnections = 0

type Server(pipeManager: IPipeManager,listenOnPort: int,tcpCount: int,minors: int,isMajorOnListen: bool) as this=
    let lockobj = new obj()
    let socketStoreMap = new Generic.Dictionary<string,Pipe>()
    let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
    let receiveEndpoint = new System.Net.IPEndPoint(IPAddress.Any,listenOnPort)
    let lockobj2 = new obj()
    let lockobj3 = new obj()
    do
        listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listeningSocket.Bind(receiveEndpoint)
        listeningSocket.Listen(10000)
    let guidReadCallback = new AsyncCallback(this.GUIDReadCallback)
    

    member this.removePi(pipe: Pipe,a: int)=
     //   Monitor.Enter lockobj3
        ignore(socketStoreMap.Remove(System.Text.Encoding.ASCII.GetString(pipe.GUID)))
//        printfn "pipes in dictionary: %i" socketStoreMap.Count
     //   printfn "total accepted: %i" totalAcceptedConnections
//        for p:Pipe in arr do
//            p.Test()
       //     printfn "%A" p.GUID
      //  Monitor.Exit lockobj3

    member this.SingleListen2()=
        while true do
            Monitor.Enter lockobj2
            let newSocket = listeningSocket.Accept()
            totalAcceptedConnections <- (totalAcceptedConnections+1)

            let newPipe = new Pipe(pipeManager,minors,isMajorOnListen)
            let newGuid = Guid.NewGuid().ToByteArray()
            socketStoreMap.Add(System.Text.Encoding.ASCII.GetString(Array.sub newGuid 0 guidSize),newPipe)
            newPipe.GUID <- newGuid
           // printfn "accepted a new connection" 
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
            newPipe.NewSocketReceived(newSocket)
            Monitor.Exit lockobj2

    member this.StartListening()=
        if  isMajorOnListen = true then
            this.SingleListen2()
            
        else
            this.MultiListen2() 
        
//    member this.SingleAccept(e: obj)=
//        this.SingleListen()
//
//        let newPipe = new Pipe(pipeManager,minors,isMajorOnListen)
//        let newGuid = Guid.NewGuid().ToByteArray()
//        socketStoreMap.Add(System.Text.Encoding.ASCII.GetString(Array.sub newGuid 0 guidSize),newPipe)
//        newPipe.GUID <- newGuid
//        let sc = e :?> SocketAsyncEventArgs
//        
//        let newSocket = sc.AcceptSocket
//        printfn "%A" sc.SocketError
//
//        printfn "accepted a new connection" 
//        newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
//        newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
//        newPipe.NewSocketReceived(newSocket)
//    
//    member this.SingleListen()=
//        let sc = new SocketAsyncEventArgs()
//        try
//            let bo = listeningSocket.AcceptAsync(sc)
//            if bo = true then
//                sc.Completed.Add(this.SingleAccept)
//                
//            
//            else
//                this.SingleAccept(sc)               
//        with
//        | _ as e-> printfn "accept exception: %A" e.Message 
             
    member this.MultiAccept(e: obj)=
        this.MultiListen()

        let sc = e :?> SocketAsyncEventArgs
        let newSocket = sc.AcceptSocket
        printfn "accepted a new connection : %A" newSocket
        newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
        newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)

        let guid = Array.create guidSize (new Byte())
        try
            ignore(newSocket.BeginReceive(guid,0,guidSize,SocketFlags.None,guidReadCallback,(guid,newSocket)))
        with
        | :? SocketException -> newSocket.Close()
        | :? ObjectDisposedException -> ()
        
    member this.MultiListen2()=
        while true do 
            let newSocket = listeningSocket.Accept()
            printfn "accepted a new connection : %A" newSocket
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)

            let guid = Array.create guidSize (new Byte())
            try
                ignore(newSocket.BeginReceive(guid,0,guidSize,SocketFlags.None,guidReadCallback,(guid,newSocket)))
            with
            | :? SocketException -> newSocket.Close()
            | :? ObjectDisposedException -> ()

    member this.MultiListen()=
        
        try
            let sc = new SocketAsyncEventArgs()
            let bo = listeningSocket.AcceptAsync(sc)
            if bo = true then
                sc.Completed.Add(this.MultiAccept)
            else
                this.MultiAccept(sc)               
        with
        | e -> printfn "%A" e.Message


    member this.GUIDReadCallback(result: IAsyncResult)=
        Monitor.Enter lockobj
        let h = result.AsyncState :?> (byte[]*Socket)
        let guid =  fst(h)
        let socket = snd(h)

        try
            let read = socket.EndReceive(result)
            if read <> guidSize then
                printfn "Couldn't read guid, handle"
            else
                if socketStoreMap.ContainsKey(System.Text.Encoding.ASCII.GetString(Array.sub guid 0 guidSize)) then
                    ()
                
                else
                    let newPipe = new Pipe(pipeManager,minors,isMajorOnListen)
                    socketStoreMap.Add(System.Text.Encoding.ASCII.GetString(Array.sub guid 0 guidSize),newPipe)
                    newPipe.GUID <- guid
                let s = socketStoreMap.[System.Text.Encoding.ASCII.GetString(Array.sub guid 0 guidSize)]
            
                s.NewSocketReceived(socket)
        with 
        | :? SocketException -> socket.Close()
        | :? ObjectDisposedException -> ()    
        Monitor.Exit lockobj


type private Client(forwardRelayAddress: IPAddress,forwardRelayPort: int,tcpCount: int,isMajorOnListen: bool) as this=
    let first (c,_,_) = c
    let second (_,c,_) = c
    let third (_,_,c) = c
//    let fourth (_,_,_,c) =c

    let callback= new AsyncCallback(this.ConnectCallback)
    let sendCallback = new AsyncCallback(this.SendCallback)
      

    member this.Connect(pipe: Pipe) =
        let s= new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
        s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
        pipe.AddPendingSocket(s)
        try
            ignore(s.BeginConnect(forwardRelayAddress,forwardRelayPort,callback,(s,pipe)))
            
        with
        | :? SocketException -> pipe.Close()
        | :? ObjectDisposedException -> ()
    member this.ConnectCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (Socket*Pipe)
        let socket = fst(h)
        let pipe = snd(h)
        try 
            socket.EndConnect(result)
            if isMajorOnListen = false then
                pipe.SocketConnected(socket)
            else
                
                ignore(socket.BeginSend(pipe.GUID,0,pipe.GUID.GetLength(0),SocketFlags.None,sendCallback,(socket,pipe)))
        with 
        | :? SocketException -> pipe.Close()
        | :? ObjectDisposedException -> ()

    member this.SendCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (Socket*Pipe)
        let socket = fst(h)
        let pipe = snd(h)
       
        try
            let sentCount = socket.EndSend(result)
            if sentCount <> guidSize then
                printfn "couldn't send guid"
            else
                pipe.SocketConnected(socket)
        with       
        | :? SocketException -> pipe.Close()
        | :? ObjectDisposedException -> ()


type Relay(listenOnPort: int,listenTcpConnectionCount: int, forwardRelayAddress: IPAddress,forwardRelayPort: int,forwardTcpConnectionCount: int,segmentSize: int, minorConnectionBufferSize: int,dynamicSegmentSize:int,dynamicMinorBufferSize:int,dynamic: bool, isMajorOnListen: bool ) as this =
    let max x y = 
        if x > y then x
        else y

    let throttleSize = 11
    let mutable monitor = null

    let server = new Server(this,listenOnPort,listenTcpConnectionCount,max listenTcpConnectionCount forwardTcpConnectionCount,isMajorOnListen)
    let client = new Client(forwardRelayAddress,forwardRelayPort,forwardTcpConnectionCount,isMajorOnListen)
//    let monitor = new Monitor(this.MonitorFired)
 //   let mutable connect = 0
    let lockobj = new obj()
    let weakReferences = Generic.List<WeakReference>()
    do  
        if isMajorOnListen && dynamic then
            monitor <- new Monitor(this,2000)
            monitor.Start()
        if (listenTcpConnectionCount = 1) || (forwardTcpConnectionCount=1) then 
            server.StartListening()
        else
            printfn "multi to multi relay not supported yet, and can be easily accomplished with two relays"

    member x.printAllWeakReferences()=  
        for s in weakReferences.ToArray() do
            printfn "is garbage collected: %b" (not s.IsAlive)
    interface IPipeManager with
        member x.needAConnection(pipe: obj) =
            let p = pipe :?> Pipe
            client.Connect(p)
            
        member x.getSegmentSize() =
            segmentSize            
        member x.getMinorSocketBufferSize()  =
            minorConnectionBufferSize
        member x.getDynamicSocketBufferSize()=
            dynamicMinorBufferSize
        member x.getDynamicSegmentSize()=
            dynamicSegmentSize
        member x.dataTransferIsAboutToBegin(pipe: IDataPipe)=
            if isMajorOnListen = true && monitor<>null then
                monitor.Add(pipe)
        
        

        member x.pipeDone(pipe: obj)=
//            totPipes <- totPipes - 1
//            printfn "pipe done, total: %i" totPipes
            Monitor.Enter lockobj
      //      printfn "Pipe done, total Transfered: %i KB" ((pipe:?>IDataPipe).TotalTransferedData()/1000UL) 
            server.removePi(pipe :?> Pipe,3)
            if monitor <> null then
                monitor.Remove(pipe :?> IDataPipe)
          //  let wr = new WeakReference(pipe)
         //   weakReferences.Add(wr)
          //  x.printAllWeakReferences()
         //   GC.Collect()
            Monitor.Exit lockobj

    interface IMonitorDelegate with
        member x.objectHasReachedActivityCriteria(pipe: obj)=
            printfn "Acceleration in progress..."
            let d = pipe :?> Pipe
            d.ThrottleUp(throttleSize)
