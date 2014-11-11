module Pipe

open System
open SocketStore
open PipeStates
open System.Net.Sockets
open System.Net
open IPipeManager
open Splitter
open Merger

type Pipe(pipeManager: IPipeManager,minorCount: int,isMajorSocketOnRelayListenSide: bool)as this=  // saves state info for each tcp pipe and implements MRelay protocol
    let mutable state = PipeState.AcceptingConnections
    let majorReadCallback = new AsyncCallback(this.DataReceiveToMajorSocket)
    let majorSendCallback = new AsyncCallback(this.DataSendToMajorSocket)
    let minorReadCallback = new AsyncCallback(this.DataReceiveToMinorSocket)
    let minorSendCallback = new AsyncCallback(this.DataSendToMinorSocket)
    let socketStore = new SocketStore(minorCount)
    let mutable merger: StreamMerger = null
    let mutable splitter: StreamSplitter = null
    let mutable guid: byte[] = null

    member this.GUID 
        with get() = guid
        and set(gui: byte[]) = guid <- gui

    member public this.NewSocketReceived(socket: Socket)= 
        match state with
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=true -> 
            state <- PipeState.Connecting
            socketStore.MajorSocket <- socket
            for i = 0 to minorCount-1 do
                pipeManager.needAConnection(this) 
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=false -> 
            this.ReadSocketIndex(socket)
        
    member public this.SocketConnected(socket: Socket)=
        match state with
        | PipeState.Connecting when isMajorSocketOnRelayListenSide= false ->
            socketStore.MajorSocket <- socket;
            this.InitRelay()
        | PipeState.Connecting when isMajorSocketOnRelayListenSide= true ->
            let index = socketStore.AddToMinorSockets(socket);
            let buf = Array.create 1 (new Byte());
            buf.[0] <- (byte) index;
            ignore(socket.BeginSend(buf,0,1,SocketFlags.None,null,null)) ;// check, do beginSend(data1,socket1);beginSend(data2,socket1) make data1 be sent first and then data2 for sure?
            if socketStore.ConnectedSockets = minorCount then
                this.InitRelay()

    member private this.DataReceiveToMajorSocket(result: IAsyncResult)=
        printfn "stub"
        

    member private this.DataSendToMajorSocket(result: IAsyncResult) =
        printfn "stub"

    member private this.DataReceiveToMinorSocket(result: IAsyncResult)=
        match state with
        | PipeState.AcceptingConnections ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket =  fst(h)
            let socketIndex = snd(h)
            let received = socket.EndReceive(result)
            if received <> 1 then
                raise(new Exception("couldn't read index"))
            else
                socketStore.AddToMinorSockets(socket,((int)(socketIndex.[0])))
                if socketStore.ConnectedSockets = minorCount then // we have received enough connections, now try to connect 
                    state <- PipeState.Connecting
                    pipeManager.needAConnection(this)
                
            
    member private this.DataSendToMinorSocket(result: IAsyncResult)=
        printfn "implement"

    
    member private this.ReadSocketIndex(socket:Socket)=
        let index = Array.create 1 (new Byte())
        ignore(socket.BeginReceive(index,0,index.GetLength(0),SocketFlags.None,minorReadCallback,(socket,index)))
    

    member private this.InitRelay()=
        state <- PipeState.Relaying
        splitter <- new StreamSplitter(socketStore,socketStore.MajorSocket,socketStore.MinorSockets,pipeManager.getSegmentSize(),pipeManager.getMinorSocketBufferSize())
        merger <- new StreamMerger(socketStore,socketStore.MajorSocket,socketStore.MinorSockets,pipeManager.getSegmentSize(),pipeManager.getMinorSocketBufferSize())

    member this.Close()= 
        printfn "stub"