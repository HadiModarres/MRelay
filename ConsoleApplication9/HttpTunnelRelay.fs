module HttpTunnelRelay


open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open Merger
open Splitter
open StreamEncryptor
open StreamDecryptor
open EncryptedPipe
open System.IO
open System.Threading.Tasks
open System.Security.Cryptography
open TunnelPipe
open HttpHeaderFactory


type HttpTunnelRelay(listenOnPort: int,forwardAddress:IPAddress,forwardPort:int,majorOnListen: bool) as this=
    
    let lockobj = new obj()
    let lockobj2 = new obj()

    let connectCallback = new AsyncCallback(this.ConnectCallback)
//    let sendCallback = new AsyncCallback(this.SendCallback)
    let postSendCallback = new AsyncCallback(this.PostSendCallback)
    let responseReadCallback = new AsyncCallback(this.GetResponseReadCallback)    


    let pipeMap = new Generic.Dictionary<string,TunnelPipe>()
    let requestReadCallback = new AsyncCallback(this.RequestReadCallback)
    let majorCallback = new AsyncCallback(this.MajorConnectCallback)
    do
        this.StartListening()

    member this.StartListening() =
        let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        
    //    listeningSocket.NoDelay <- true
        let receiveEndpoint = new System.Net.IPEndPoint(IPAddress.Any,listenOnPort)
        
        try
            listeningSocket.Bind(receiveEndpoint)
            listeningSocket.Listen(10000)

            while true do    
                let receivedSocket = listeningSocket.Accept()
                if majorOnListen then
                    this.initiateConnect(receivedSocket)
                else
                    let buf = Array.create 200 0uy
                    ignore(receivedSocket.BeginReceive(buf,0,200,SocketFlags.None,requestReadCallback,(receivedSocket,buf)))    

                
                
        with
        | e -> printfn "Failed to start http tunnel: %A" e.Message

    member this.initiateConnect(majorSock: Socket)=
        let inboundSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        let outboundSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        let newPipe = new TunnelPipe()
        newPipe.InboundSocket <- inboundSocket
        newPipe.OutboundSocket <- outboundSocket
        newPipe.MajorSocket <- majorSock
        newPipe.MajorReady <- true
        try
             ignore(inboundSocket.BeginConnect(forwardAddress,forwardPort,connectCallback,(newPipe,true)))
             ignore(outboundSocket.BeginConnect(forwardAddress,forwardPort,connectCallback,(newPipe,false)))
            
        with
        | _  -> newPipe.Close()

        
    member this.ConnectCallback(result: IAsyncResult)=
        Monitor.Enter lockobj
        let h = result.AsyncState :?> (TunnelPipe*Boolean)
        let pipe = fst(h)
        let inbound = snd(h)
        try 
            
            if  inbound = true then
                pipe.InboundSocket.EndConnect(result)
                this.SendGetRequest(pipe)
            else
                pipe.OutboundSocket.EndConnect(result)
                this.SendPostRequest(pipe)
                
        with 
        | _  -> pipe.Close()
        
        Monitor.Exit lockobj

    member this.SendPostRequest(pipe: TunnelPipe)=
        try
            let k = HeaderFactory.GuidtoPost(pipe.Guid)
            ignore(pipe.OutboundSocket.BeginSend(HttpHeaderFactory.HeaderFactory.GuidtoPost(pipe.Guid),0,HttpHeaderFactory.HeaderFactory.GuidtoPost(pipe.Guid).GetLength(0),SocketFlags.None,postSendCallback,pipe))
        with 
        | _  -> pipe.Close()
        
    member this.SendGetRequest(pipe: TunnelPipe)=
        try
            ignore(pipe.InboundSocket.BeginSend(HttpHeaderFactory.HeaderFactory.GuidToHttp(pipe.Guid),0,HttpHeaderFactory.HeaderFactory.GuidToHttp(pipe.Guid).GetLength(0),SocketFlags.None,null,null))
            let resp = Array.create (HeaderFactory.getResponseSize()) 0uy
            ignore(pipe.InboundSocket.BeginReceive(resp,0,resp.GetLength(0),SocketFlags.None,responseReadCallback,pipe))
        with
        | _ -> pipe.Close()
    member this.PostSendCallback(result: IAsyncResult)=
        let pipe = result.AsyncState :?> TunnelPipe
        try
            ignore(pipe.OutboundSocket.EndSend(result))
            pipe.OutboundReady<-true
        with 
        | _ -> pipe.Close() 
         
    member this.GetResponseReadCallback(result: IAsyncResult)=
        let pipe = result.AsyncState :?> TunnelPipe
        try
            ignore(pipe.InboundSocket.EndReceive(result))
            pipe.InboundReady<- true
        with 
        | _ -> pipe.Close() 
        

    member this.RequestReadCallback(result: IAsyncResult)=
        Monitor.Enter lockobj
        let state = result.AsyncState :?> (Socket*byte[])
        let socket = fst(state) 
        let buf = snd(state)
        
        try
            ignore(socket.EndReceive(result))
            if HeaderFactory.IsPost(buf) then
                let guid = HeaderFactory.PostToGuid(System.Text.Encoding.ASCII.GetString(buf))
                if this.ExistsPipeWithID(guid) then
                    let (pipe:TunnelPipe) = this.GetPipeWithID(guid)
                    pipe.InboundSocket <- socket
                    pipe.InboundReady <- true
                else
                    let newPipe = new TunnelPipe()
                    newPipe.InboundSocket <- socket
                    newPipe.InboundReady <- true                    
                    this.AddPipeWithID(newPipe,guid)
                    this.InitiateMajor(newPipe)
            else
                let guid = HeaderFactory.HttpToGuid(System.Text.Encoding.ASCII.GetString(buf))
                if this.ExistsPipeWithID(guid) then
                    let (pipe:TunnelPipe) = this.GetPipeWithID(guid)
                    pipe.OutboundSocket <- socket
                    this.SendGetResponse(pipe)
                else
                    let newPipe = new TunnelPipe()
                    newPipe.OutboundSocket <- socket
                    this.InitiateMajor(newPipe)
                    this.SendGetResponse(newPipe)
                    this.AddPipeWithID(newPipe,guid)
                
        with 
        | _ -> socket.Close() 

        Monitor.Exit lockobj
    
    member this.ExistsPipeWithID(guid: string)=    
        let y = pipeMap.ContainsKey(guid)
        y
    member this.GetPipeWithID(guid: string)=
        pipeMap.[guid]
    member this.AddPipeWithID(pipe: TunnelPipe,guid:string)=
       // let newGuid =System.Text.Encoding.ASCII.GetString(Guid.NewGuid().ToByteArray())
        pipeMap.Add(guid,pipe)
        


    member this.InitiateMajor(pipe: TunnelPipe)=
        let majorSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        pipe.MajorSocket <- majorSocket
        try
            ignore(majorSocket.BeginConnect(forwardAddress,forwardPort,majorCallback,(pipe)))
        with
        | _ -> pipe.Close()
    member this.SendGetResponse(pipe: TunnelPipe)=
        try
            ignore(pipe.OutboundSocket.BeginSend(HeaderFactory.GetResponseHeader(),0,HeaderFactory.getResponseSize(),SocketFlags.None,null,null))
            pipe.OutboundReady <- true
        with
        |_ -> pipe.Close()
    member this.MajorConnectCallback(result: IAsyncResult)=
        let pipe = result.AsyncState :?> TunnelPipe
        try
           pipe.MajorSocket.EndConnect(result)     
           pipe.MajorReady <- true
        with 
        | _  -> pipe.Close()
        