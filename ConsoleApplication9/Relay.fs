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
module Relay

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open Merger
open Splitter
open SocketStore

//let readFakeHttpRequest = true
//let sendFakeHttpRequest = true
let decryptReceive = false
let encryptSend = false
let fakeHttpRequest = System.Text.Encoding.ASCII.GetBytes("GET /path/file.iso HTTP/1.0\r\nFrom: someuser@jmarshall.com\r\nUser-Agent: HTTPTool/1.0\r\n\r\n")
let fakeHttpResponse = System.Text.Encoding.ASCII.GetBytes("HTTP/1.0 200 OK\r\nDate: Fri, 31 Dec 1999 23:59:59 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 98765123\r\n\r\n")

type private Server(listenOnPort: int,tcpCount: int,newConnectionReceived: SocketStore -> unit,minors: int,exchangeFakeHeader: bool) as this=
    let lockobj = new obj()
    let f (s:SocketStore) = 
        printfn "dummy"
   
    
    let socketStoreMap = new Generic.Dictionary<string,SocketStore>()

    let mutable callback: SocketStore -> unit = f
    let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
    let receiveEndpoint = new System.Net.IPEndPoint(IPAddress.Any,listenOnPort)
    
    do
        callback <- newConnectionReceived
        listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listeningSocket.Bind(receiveEndpoint)
        listeningSocket.Listen(10000)

    let headerReadCallback = new AsyncCallback(this.HeaderReadCallback)
    let headerSendCallback = new AsyncCallback(this.HeaderSendCallback)  

    let headerReadCallback2 = new AsyncCallback(this.HeaderReadCallback2)
    let headerSendCallback2 = new AsyncCallback(this.HeaderSendCallback2)
    member this.StartListening()=
        if tcpCount=1 then
        //    printfn "single listen"
            this.SingleListen()
        else
       //     printfn "multi listen"
            this.MultiListen() 

        
    member this.SingleAccept(e: obj)=
        let newSet = new SocketStore(minors)
        let sc = e :?> SocketAsyncEventArgs
        let newSocket = sc.AcceptSocket
        newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
        newSet.MajorSocket <- newSocket
        if exchangeFakeHeader = true then
            ignore(newSocket.BeginReceive(fakeHttpRequest,0,fakeHttpRequest.GetLength(0),SocketFlags.None,headerReadCallback2,(newSet,newSocket)))
        else
            ignore(callback(newSet))
        this.SingleListen()
    
    member this.SingleListen()=

//        let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
//        let receiveEndpoint = new System.Net.IPEndPoint(IPAddress.Any,listenOnPort)
//        listeningSocket.Bind(receiveEndpoint)
//        listeningSocket.Listen(10000)
      //  listeningSocket.LingerState.Enabled <- false
        try
            let newSet = new SocketStore(minors)
            let sc = new SocketAsyncEventArgs()
            let bo = listeningSocket.AcceptAsync(sc)
            if bo = true then
                sc.Completed.Add(this.SingleAccept)
            else
                this.SingleAccept(sc)               
        with
        | e -> listeningSocket.Dispose();listeningSocket.Close();
//            let newSocket = listeningSocket.Accept()
//            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
//            newSet.MajorSocket <- newSocket
//            if exchangeFakeHeader = true then
//                ignore(newSocket.BeginReceive(fakeHttpRequest,0,fakeHttpRequest.GetLength(0),SocketFlags.None,headerReadCallback2,(newSet,newSocket)))
//            else
//                ignore(callback(newSet))
             

        
    member this.MultiListen()=
        
        let callback = new AsyncCallback(this.GUIDReadCallback)
        
        while true do

            let newSocket = listeningSocket.Accept()
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
      //      printfn "ACCEPTED A NEW CONNECTION"
      //      newSocket.SendBufferSize <- (256*1024)
      //      newSocket.ReceiveBufferSize <- (256*1024)
            let guid = Array.create 17 (new Byte())
            ignore(newSocket.BeginReceive(guid,0,17,SocketFlags.None,callback,(guid,newSocket)))

    member this.GUIDReadCallback(result: IAsyncResult)=
        Monitor.Enter lockobj
        let h = result.AsyncState :?> (byte[]*Socket)
        let guid =  fst(h)
        let socket = snd(h)

        
  //      printfn "guid: %A" guid 
        let read = socket.EndReceive(result)
        if read <> 17 then
            printfn "Couldn't read guid, handle"
        else
    //        printfn "key part: %A" (Array.sub guid 0 16)
            if socketStoreMap.ContainsKey(System.Text.Encoding.ASCII.GetString(Array.sub guid 0 16)) then
     //           printfn "map contains key"
    //            printfn "%A" (System.Text.Encoding.ASCII.GetString(Array.sub guid 0 16))
                ()
          //      if  exchangeFakeHeader = true then
           //         ignore(socket.BeginReceive(fakeHttpRequest,0,fakeHttpRequest.GetLength(0),SocketFlags.None,headerReadCallback,result.AsyncState))
          //      else
              //  s.AddToMinorSockets(socket,int(guid.[16]))
                
            else
        //        printfn "map doesnt contain key"
                let newStore = new SocketStore(minors)
                socketStoreMap.Add(System.Text.Encoding.ASCII.GetString(Array.sub guid 0 16),newStore)

            let s = socketStoreMap.[System.Text.Encoding.ASCII.GetString(Array.sub guid 0 16)]
            if exchangeFakeHeader = true then
                 ignore(socket.BeginReceive(fakeHttpRequest,0,fakeHttpRequest.GetLength(0),SocketFlags.None,headerReadCallback,(guid,socket)))
            else
                s.AddToMinorSockets(socket,int(guid.[16]))
                if s.ConnectedSockets = tcpCount then
                        do callback(s)

        Monitor.Exit lockobj

    member this.HeaderReadCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (byte[]*Socket)
        let guid =  fst(h)
        let socket = snd(h)
        let read = socket.EndReceive(result)
        if read <> fakeHttpRequest.GetLength(0) then
            printfn "couldn't read fake header, handle"
        else
            ignore(socket.BeginSend(fakeHttpResponse,0,fakeHttpResponse.GetLength(0),SocketFlags.None,headerSendCallback,(guid,socket)))
    
    member this.HeaderReadCallback2(result: IAsyncResult)=
        let h = result.AsyncState :?> (SocketStore*Socket)
        let socketStore =  fst(h)
        let socket = snd(h)
        let read = socket.EndReceive(result)
        if read <> fakeHttpRequest.GetLength(0) then
            printfn "couldn't read fake header, handle"
        else
            printfn "fake http request read done, sending response ..."
            ignore(socket.BeginSend(fakeHttpResponse,0,fakeHttpResponse.GetLength(0),SocketFlags.None,headerSendCallback2,(socketStore,socket)))

    member this.HeaderSendCallback2(result: IAsyncResult)=
        let h = result.AsyncState :?> (SocketStore*Socket)
        let socketStore =  fst(h)
        let socket = snd(h)
        let  sent = socket.EndSend(result)
        if sent <> fakeHttpResponse.GetLength(0) then
            printfn "couldn't send fake header, handle"
            
        else
            printfn "fake response sent. callback .."
            ignore(callback(socketStore))

    member this.HeaderSendCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (byte[]*Socket)
        let guid =  fst(h)
        let socket = snd(h)
        let  sent = socket.EndSend(result)
        if sent <> fakeHttpResponse.GetLength(0) then
            printfn "couldn't send fake header, handle"
            
        else
            let s = socketStoreMap.[System.Text.Encoding.ASCII.GetString(Array.sub guid 0 16)]
            s.AddToMinorSockets(socket,int(guid.[16]))
            if s.ConnectedSockets = tcpCount then
                do callback(s)

type private Client(forwardRelayAddress: IPAddress,forwardRelayPort: int,tcpCount: int,connectionEstablished: SocketStore -> unit, exchangeFakeHeader: bool) as this=
    let first (c,_,_,_) = c
    let second (_,c,_,_) = c
    let third (_,_,c,_) = c
    let fourth (_,_,_,c) =c

    let callback= new AsyncCallback(this.ConnectCallback)
    let sendCallback = new AsyncCallback(this.SendCallback)
    let headerSendCallback = new AsyncCallback(this.HeaderSendCallback)
    let headerReceiveCallback = new AsyncCallback(this.HeaderReceiveCallback)
    member this.Connect(socketStore: SocketStore)=
        if tcpCount = 1 then
            this.SingleConnect(socketStore)
        else
            this.MultiConnect(socketStore)


    member this.SingleConnect(socketStore: SocketStore) =
        let s= new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
        let t = Array.create 0 (new Byte())
        try
            ignore(s.BeginConnect(forwardRelayAddress,forwardRelayPort,callback,(s,socketStore,t,0)))
        with 
        | :? SocketException -> socketStore.Close()
        | :? ObjectDisposedException -> ()  
    member this.MultiConnect(socketStore: SocketStore) =
    //    printfn "MULTI CONNECT CALLED"
        let newGuid = Guid.NewGuid().ToByteArray()
       
        for i = 0 to (tcpCount-1) do
            let s= new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
            s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
            try
            ignore(s.BeginConnect(forwardRelayAddress,forwardRelayPort,callback,(s,socketStore,newGuid,i)))
            with 
            | :? SocketException as se->printfn "%A" se.Message; socketStore.Close()
            | :? ObjectDisposedException -> ()  

    member this.ConnectCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (Socket*SocketStore*byte[]*int)
        let socket = first(h)
        let socketStore = second(h)
        let guid = third(h)
        let index = fourth(h)
        try 
            socket.EndConnect(result)
            if tcpCount = 1 then // we don't need guid mechanism when there is only one connection
                    if socketStore.MajorSocket = null then
                        socketStore.MajorSocket <- socket
                    else
                        socketStore.AddToMinorSockets(socket,index)
                        
                    if exchangeFakeHeader = true then 
                        ignore(socket.BeginSend(fakeHttpRequest,0,fakeHttpRequest.GetLength(0),SocketFlags.None,headerSendCallback,(socket,socketStore,guid,index)))
                    else
                        do connectionEstablished(socketStore)

            else
                let crew = Array.create 1 (new Byte())
                crew.[0] <- byte(index)
                let guidAndIndex = Array.append guid crew
                ignore(socket.BeginSend(guidAndIndex,0,guidAndIndex.GetLength(0),SocketFlags.None,sendCallback,(socket,socketStore,guid,index)))
        with 
        | :? SocketException -> socketStore.Close()
        | :? ObjectDisposedException -> ()

    member this.SendCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (Socket*SocketStore*byte[]*int)
        let socket = first(h)
        let socketStore = second(h)
        let guid = third(h)
        let index = fourth(h)
  //      printfn "sent guid: %A" guid
        try
            let sentCount = socket.EndSend(result)
            if sentCount <> 17 then
                printfn "couldn't send guid"
            else
              //  socketStore.AddMinorSocket(socket)
                if exchangeFakeHeader = true then
                    ignore(socket.BeginSend(fakeHttpRequest,0,fakeHttpRequest.GetLength(0),SocketFlags.None,headerSendCallback,result.AsyncState))
                else
                    socketStore.AddToMinorSockets(socket,index)
                    if socketStore.ConnectedSockets = tcpCount then // we have enough tcp connections
                        do connectionEstablished(socketStore)
        with 
        | :? SocketException -> socketStore.Close()
        | :? ObjectDisposedException -> ()

    member this.HeaderSendCallback(result: IAsyncResult) =
        let h = result.AsyncState :?> (Socket*SocketStore*byte[]*int)
        let socket = first(h)
        let socketStore = second(h)
        let guid = third(h)
        let index = fourth(h)
        try
            let sentCount = socket.EndSend(result)
            if sentCount <> fakeHttpRequest.GetLength(0) then
                printfn "couldn't send fake request, handle"
            else
                printfn "fake request sent, reading response ... "
                ignore(socket.BeginReceive(fakeHttpResponse,0,fakeHttpResponse.GetLength(0),SocketFlags.None,headerReceiveCallback,(socket,socketStore,guid,index)))
        with 
        | :? SocketException -> socketStore.Close()
        | :? ObjectDisposedException -> ()

    member this.HeaderReceiveCallback(result: IAsyncResult) =
        let h = result.AsyncState :?> (Socket*SocketStore*byte[]*int)
        let socket = first(h)
        let socketStore = second(h)
        let guid = third(h)
        let index = fourth(h)
        try
            let receiveCount = socket.EndReceive(result)
        
            if receiveCount <> fakeHttpResponse.GetLength(0) then
                printfn "couldn't read fake response, handle"
            else
                printfn "response received. callback ..."
                if tcpCount = 1 then
                //    printfn "connection established"
                    do connectionEstablished(socketStore)
                else
                    socketStore.AddToMinorSockets(socket,index)
                    if socketStore.ConnectedSockets = tcpCount then // we have enough tcp connections
                        do connectionEstablished(socketStore)
        with 
        | :? SocketException -> socketStore.Close()
        | :? ObjectDisposedException -> ()

type Relay(listenOnPort: int,listenTcpConnectionCount: int, forwardRelayAddress: IPAddress,forwardRelayPort: int,forwardTcpConnectionCount: int,segmentSize: int, minorConnectionBufferSize: int,readFakeRequest: bool,sendFakeRequest: bool ) as this =
    let max x y = 
        if x > y then x
        else y

    let server = new Server(listenOnPort,listenTcpConnectionCount,this.connectionReceivedCallback,max listenTcpConnectionCount forwardTcpConnectionCount,readFakeRequest)
    let client = new Client(forwardRelayAddress,forwardRelayPort,forwardTcpConnectionCount,this.connectionEstablishedCallback,sendFakeRequest)
    let mutable connect = 0
    
    do  
      //  printfn "initiating multi relay"
        if (listenTcpConnectionCount = 1) || (forwardTcpConnectionCount=1) then 
      //      printfn "starting relay"
            server.StartListening()
        else
            printfn "multi to multi relay not supported yet, and can be easily accomplished with two relays"

    member this.connectionReceivedCallback(socketStore: SocketStore)=
        client.Connect(socketStore)


    member this.connectionEstablishedCallback(socketStore: SocketStore) =
        //    printfn "creating merger and splitter"
      //      printfn "%A" socketStore.MajorSocket.LocalEndPoint
      //      printfn "%A" socketStore.MinorSockets.[0].LocalEndPoint
            ignore(new StreamMerger(socketStore,segmentSize,minorConnectionBufferSize))
            ignore(new StreamSplitter(socketStore,segmentSize,minorConnectionBufferSize))
        















