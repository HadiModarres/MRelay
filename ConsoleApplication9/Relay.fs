﻿// Copyright 2014 Hadi Modarres
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
open HttpHeaderFactory

let guidSize = 16 // size of guid in bytes

type Server(pipeManager: IPipeManager,listenOnPort: int,tcpCount: int,minors: int,isMajorOnListen: bool) as this=
    
    let socketStoreMap = new Generic.Dictionary<string,Pipe>()
    let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
    let receiveEndpoint = new System.Net.IPEndPoint(IPAddress.Any,listenOnPort)
    let guidReadCallback = new AsyncCallback(this.GUIDReadCallback)
    let responseSendCallback = new AsyncCallback(this.ResponseSendCallback)
    let lockobj = new obj()
    let lockobj2 = new obj()
    do
        try
            listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listeningSocket.Bind(receiveEndpoint)
            listeningSocket.Listen(10000)
        with
        | _ as e -> printfn "Failed to start relay: %A" e.Message

    member this.removePi(pipe: Pipe,a: int)=
        Monitor.Enter lockobj2
        ignore(socketStoreMap.Remove(pipe.GUID))
        printfn "pipes in dictionary: %i" socketStoreMap.Count
        Monitor.Exit lockobj2
     //   printfn "total accepted: %i" totalAcceptedConnections
//        for p:Pipe in arr do
//            p.Test()
       //     printfn "%A" p.GUID
      //  Monitor.Exit lockobj3

    member this.SingleListen()=
        while true do
            try
                let mutable newSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
                newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
                newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
                newSocket <- listeningSocket.Accept()
                let newPipe = new Pipe(pipeManager,minors,isMajorOnListen)
                let newGuid = Guid.NewGuid().ToByteArray()
                let gu = BitConverter.ToString(newGuid)
                socketStoreMap.Add(gu,newPipe)
                newPipe.GUID <- gu
               // printfn "accepted a new connection" 
                newPipe.NewSocketReceived(newSocket)
            with 
            |_ -> ()
    member this.StartListening()=
        if  isMajorOnListen = true then
            this.SingleListen()
            
        else
            this.MultiListen() 
        
               
        
    member this.MultiListen()=
        while true do 
            let mutable newSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
            newSocket <- listeningSocket.Accept()
            let request= Array.create (HeaderFactory.GetRequestSizeTest()) 0uy
            try
                ignore(newSocket.BeginReceive(request,0,request.GetLength(0),SocketFlags.None,guidReadCallback,(request,newSocket)))
            with
            | _ -> newSocket.Close()
            
            

    


    member this.GUIDReadCallback(result: IAsyncResult)=
        Monitor.Enter lockobj
        let h = result.AsyncState :?> (byte[]*Socket)
        let request =  fst(h)
        let socket = snd(h)

        try
            let read = socket.EndReceive(result)
            if read <> HeaderFactory.GetRequestSizeTest() then
                socket.Close()
            else
                let gu = HeaderFactory.HttpToGuidTest(System.Text.Encoding.ASCII.GetString(request))
                if socketStoreMap.ContainsKey(gu) then
                    ()
                
                else
                    let newPipe = new Pipe(pipeManager,minors,isMajorOnListen)
                    socketStoreMap.Add(gu,newPipe)
                    newPipe.GUID <- gu
                let s = socketStoreMap.[gu]
                let b = HeaderFactory.GetResponseHeaderTest()
                try
                    ignore(socket.BeginSend(b,0,b.GetLength(0),SocketFlags.None,null,null))
                with
                | _ -> s.Close();socket.Close()
                s.NewSocketReceived(socket)
        with 
        | _ -> socket.Close()
            
        Monitor.Exit lockobj
    member this.ResponseSendCallback(result: IAsyncResult)=
        ()

type private Client(forwardRelayAddress: IPAddress,forwardRelayPort: int,tcpCount: int,isMajorOnListen: bool) as this=
    let first (c,_,_) = c
    let second (_,c,_) = c
    let third (_,_,c) = c
//    let fourth (_,_,_,c) =c

    let callback= new AsyncCallback(this.ConnectCallback)
    let sendCallback = new AsyncCallback(this.SendCallback)
    let responseReceiveCallback = new AsyncCallback(this.ResponseReadCallback)

    member this.Connect(pipe: Pipe) =
        let s= new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
        s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
        pipe.AddPendingSocket(s)
        try
            ignore(s.BeginConnect(forwardRelayAddress,forwardRelayPort,callback,(s,pipe)))
            
        with
        | _  -> pipe.Close()
        
    member this.ConnectCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (Socket*Pipe)
        let socket = fst(h)
        let pipe = snd(h)
        try 
            socket.EndConnect(result)
            if isMajorOnListen = false then
                pipe.SocketConnected(socket)
            else
                let b = HeaderFactory.GuidToHttpTest(pipe.GUID)
               // let k = b.GetLength(0)
                ignore(socket.BeginSend(b,0,b.GetLength(0),SocketFlags.None,sendCallback,(socket,pipe)))
        with 
        | _  -> pipe.Close()
        

    member this.SendCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (Socket*Pipe)
        let socket = fst(h)
        let pipe = snd(h)
       
        try
            let sentCount = socket.EndSend(result)
            if sentCount <> HeaderFactory.GetRequestSizeTest() then
                pipe.Close()
            else
                let b = Array.create (HeaderFactory.getResponseSizeTest()) 0uy
            //    try
                ignore(socket.BeginReceive(b,0,b.GetLength(0),SocketFlags.None,responseReceiveCallback,(socket,pipe)))
             //   with
             //   | _ -> pipe.Close()
        with       
        | _ -> pipe.Close()
        
    member this.ResponseReadCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (Socket*Pipe)
        let socket = fst(h)
        let pipe = snd(h)
        try
            let readCount = socket.EndReceive(result)
            if readCount <> HeaderFactory.getResponseSizeTest() then
                pipe.Close()
            else
                pipe.SocketConnected(socket)
        with
        |_ -> pipe.Close()
       
type Relay(listenOnPort: int,listenTcpConnectionCount: int, forwardRelayAddress: IPAddress,forwardRelayPort: int,forwardTcpConnectionCount: int,segmentSize: int, minorConnectionBufferSize: int,dynamicSegmentSize:int,dynamicMinorBufferSize:int,dynamic: bool,dynamicThrottleSize: int, isMajorOnListen: bool ) as this =
    let max x y = 
        if x > y then x
        else y

   // let throttleSize = 8
    let mutable monitor = null

    let server = new Server(this,listenOnPort,listenTcpConnectionCount,max listenTcpConnectionCount forwardTcpConnectionCount,isMajorOnListen)
    let client = new Client(forwardRelayAddress,forwardRelayPort,forwardTcpConnectionCount,isMajorOnListen)
//    let monitor = new Monitor(this.MonitorFired)
 //   let mutable connect = 0
    let lockobj = new obj()
    let weakReferences = Generic.List<WeakReference>()
    do  
        if isMajorOnListen && dynamic then
            monitor <- new Monitor(this,1500)
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
            printfn "Accelerating..."
            let d = pipe :?> Pipe
            d.ThrottleUp(dynamicThrottleSize)
