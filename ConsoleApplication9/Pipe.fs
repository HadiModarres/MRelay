module Pipe

open System
open System.Threading
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
    let mutable throttleUpSize = 0
    let timerCallback = new TimerCallback(this.ThrottleTest)
    let timer = new Threading.Timer(timerCallback)
    do 
        ignore(timer.Change(4000,Timeout.Infinite))
    
    member this.ThrottleTest(timerObj: obj)=
        this.ThrottleUp(80)
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

        | PipeState.Relaying when isMajorSocketOnRelayListenSide=false ->
            state <- PipeState.ThrottlingUp_ExchangingInfo
            let buf = Array.create 12 (new Byte())
            ignore(socket.BeginReceive(buf,0,12,SocketFlags.None,minorReadCallback,(socket,buf)))
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide = false ->
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
            if socketStore.ConnectedSockets = socketStore.MinorSockets.GetLength(0) then
                this.InitRelay()
        | PipeState.ThrottlingUp_ConnectingFirstConnection when isMajorSocketOnRelayListenSide = true ->
            socketStore.GrowMinorArray(throttleUpSize)
            let index = socketStore.AddToMinorSockets(socket)
            let size = throttleUpSize
            let sa = splitter.TotalData
            let buf = Array.append (BitConverter.GetBytes(size)) (BitConverter.GetBytes(sa))
            state <- PipeState.ThrottlingUp_ExchangingInfo
            ignore(socket.BeginSend(buf,0,buf.GetLength(0),SocketFlags.None,null,null))
            let buf2 = Array.create (8) (new Byte())
            ignore(socket.BeginReceive(buf2,0,buf2.GetLength(0),SocketFlags.None,minorReadCallback,(socket,buf2)))

        | PipeState.ThrottlingUp_ConnectingAll when isMajorSocketOnRelayListenSide = true ->
            let index = socketStore.AddToMinorSockets(socket);
            let buf = Array.create 1 (new Byte());
            buf.[0] <- (byte) index;
            ignore(socket.BeginSend(buf,0,1,SocketFlags.None,null,null)) ;  // check, do beginSend(data1,socket1);beginSend(data2,socket1) make data1 be sent first and then data2 for sure?
            if socketStore.ConnectedSockets = minorCount then
                splitter.UpdateMinorSockets(socketStore.MinorSockets)
                merger.UpdateMinorSockets(socketStore.MinorSockets)
                state <- PipeState.Relaying
                splitter.Resume()
                merger.Resume()

    member public this.ThrottleUp(byHowManyConnections: int)=
        match state with
        |PipeState.Relaying when isMajorSocketOnRelayListenSide= true ->
            throttleUpSize <- byHowManyConnections
            state <- PipeState.ThrottlingUp_PausingSplitter 
            splitter.Pause(this.SplitterPaused)
        //| a -> raise(new Exception("pipe: Throttle up requested when not relaying or when major not on listen side"))
        | _ -> ()
    member private this.SplitterPaused()=
        // check state, ToDo
        match state with
        | PipeState.ThrottlingUp_PausingSplitter when isMajorSocketOnRelayListenSide= true ->
            state <- PipeState.ThrottlingUp_ConnectingFirstConnection
            pipeManager.needAConnection(this)
        | PipeState.ThrottlingUp_PausingSplitter when isMajorSocketOnRelayListenSide= false ->
            //state <- PipeState.ThrottlingUp_ExchangingInfo
            let socket = socketStore.MinorSockets.[socketStore.MinorSockets.GetLength(0)-throttleUpSize]
            state <- PipeState.ThrottlingUp_ConnectingAll
            ignore(socket.BeginSend(BitConverter.GetBytes(splitter.TotalData),0,4,SocketFlags.None,null,null))
            
            
    member private this.MergerPaused()=
        // check State, ToDo
        match state with
        | PipeState.ThrottlingUp_PausingMerger when isMajorSocketOnRelayListenSide = true ->
            state <- PipeState.ThrottlingUp_ConnectingAll
            for i = 1 to (throttleUpSize-1) do
                pipeManager.needAConnection(this)
        | PipeState.ThrottlingUp_PausingMerger when isMajorSocketOnRelayListenSide = false ->
            state <- PipeState.ThrottlingUp_PausingSplitter
            splitter.Pause(this.SplitterPaused)
                
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
        | PipeState.ThrottlingUp_ExchangingInfo when isMajorSocketOnRelayListenSide = true ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket = fst(h)
            let pa = snd(h)
            let readCount = socket.EndReceive(result)
            if readCount <> pa.GetLength(0) then
                printfn "couldn't read data"
            else
                state <- PipeState.ThrottlingUp_PausingMerger
                let pausedAt = BitConverter.ToUInt64(pa,0)
                merger.Pause(pausedAt,this.MergerPaused)
        | PipeState.ThrottlingUp_ExchangingInfo when isMajorSocketOnRelayListenSide = false ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket = fst(h)
            let pa = snd(h)
            let readCount = socket.EndReceive(result)
            if readCount <> pa.GetLength(0) then
                printfn "couldn't read data"
            else  
                state <- PipeState.ThrottlingUp_PausingMerger
                let thu = BitConverter.ToInt32(pa,0)
                throttleUpSize <- thu
                let pauseAt = BitConverter.ToUInt64(pa,4)
                socketStore.GrowMinorArray(thu)
                let index = socketStore.AddToMinorSockets(socket)
                merger.Pause(pauseAt,this.MergerPaused)
        | PipeState.ThrottlingUp_ConnectingAll when isMajorSocketOnRelayListenSide = false ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket =  fst(h)
            let socketIndex = snd(h)
            let received = socket.EndReceive(result)
            if received <> 1 then
                raise(new Exception("couldn't read index"))
            else
                socketStore.AddToMinorSockets(socket,((int)(socketIndex.[0])))
                if socketStore.ConnectedSockets = socketStore.MinorSockets.GetLength(0) then // we have received enough connections, now try to connect 
                    splitter.UpdateMinorSockets(socketStore.MinorSockets)
                    merger.UpdateMinorSockets(socketStore.MinorSockets)
                    state <- PipeState.Relaying
                    splitter.Resume()
                    merger.Resume()

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