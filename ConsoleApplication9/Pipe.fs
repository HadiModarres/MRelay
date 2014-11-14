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
open System.Collections
open ICycle
open CycleManager

type Pipe(pipeManager: IPipeManager,minorCount: int,isMajorSocketOnRelayListenSide: bool)as this=  // saves state info for each tcp pipe and implements MRelay protocol
    let mutable state = PipeState.AcceptingConnections
    let majorReadCallback = new AsyncCallback(this.DataReceiveToMajorSocket)
    let majorSendCallback = new AsyncCallback(this.DataSendToMajorSocket)
    let minorReadCallback = new AsyncCallback(this.DataReceiveToMinorSocket)
    let minorSendCallback = new AsyncCallback(this.DataSendToMinorSocket)
    let socketStore = new SocketStore()
    let mutable guid: byte[] = null
    let mutable throttleUpSize = 0
    let mergerChain = new CycleManager()
    let splitterChain = new CycleManager()
    let timerCallback = new TimerCallback(this.ThrottleTest)
    let timer = new Threading.Timer(timerCallback)
    let mutable cycleCount = 0
    let lockobj = new obj()
    do
        socketStore.AddMinorSet(minorCount)

//    do 
//        ignore(timer.Change(4000,Timeout.Infinite))
//    
    member this.ThrottleTest(timerObj: obj)=
        ignore(this.ThrottleUpSend(30))
    member this.GUID 
        with get() = guid
        and set(gui: byte[]) = guid <- gui

    member public this.NewSocketReceived(socket: Socket)= 
        Monitor.Enter lockobj
        match state with
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=true -> 
            state <- PipeState.Connecting
            socketStore.MajorSocket <- socket
            for i = 0 to minorCount-1 do
                pipeManager.needAConnection(this) 
            ()
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=false -> 
            this.ReadSocketIndex(socket)
            ()
        | PipeState.Relaying when isMajorSocketOnRelayListenSide=false ->
            state <- PipeState.ThrottlingUp_ReadingThrottleCommand
            let buf = Array.create 1 (new Byte())
            ignore(socket.BeginReceive(buf,0,buf.GetLength(0),SocketFlags.None,minorReadCallback,(socket,buf)))
            ()
        | PipeState.ThrottlingUp_ConnectingAll when isMajorSocketOnRelayListenSide = false ->
            ignore(this.ReadSocketIndex(socket))
            ()
        | _ -> ()
        Monitor.Exit lockobj

    member public this.SocketConnected(socket: Socket)=
        Monitor.Enter lockobj
        match state with
        | PipeState.Connecting when isMajorSocketOnRelayListenSide= false ->
            socketStore.MajorSocket <- socket;
            this.InitRelay()
            ()
        | PipeState.Connecting when isMajorSocketOnRelayListenSide= true ->
            let index = socketStore.AddMinorSocket(socket);
            let buf = Array.create 1 (new Byte());
            buf.[0] <- (byte) index;
            ignore(socket.BeginSend(buf,0,1,SocketFlags.None,null,null)) ;// check, do beginSend(data1,socket1);beginSend(data2,socket1) make data1 be sent first and then data2 for sure?
            if socketStore.ConnectedSockets = socketStore.GetLastMinorSet().GetLength(0) then
                this.InitRelay()
            ()
        | PipeState.ThrottlingUp_ConnectingFirstConnection when isMajorSocketOnRelayListenSide = true ->
            state <- PipeState.ThrottlingUp_SendingPauseCommand
            socketStore.AddMinorSet(throttleUpSize)
            let index = socketStore.AddMinorSocket(socket)
            let size = throttleUpSize
            let buf = Array.create 7 (new Byte())
            let sync = splitterChain.CycleNum
            buf.[0] <- (byte) 0
            buf.[1] <- (byte) index
            Array.blit (BitConverter.GetBytes(sync)) 0 buf 2 4
            buf.[6] <- (byte) throttleUpSize
            ignore(socket.BeginSend(buf,0,buf.GetLength(0),SocketFlags.None,null,null))
            state <- PipeState.ThrottlingUp_ReceivingPauseResponse
            let buf2 = Array.create (1) (new Byte())
            ignore(socket.BeginReceive(buf2,0,buf2.GetLength(0),SocketFlags.None,minorReadCallback,(socket,buf2)))
            ()
        | PipeState.ThrottlingUp_ConnectingAll when isMajorSocketOnRelayListenSide = true ->
            let index = socketStore.AddMinorSocket(socket);
            let buf = Array.create 1 (new Byte());
            buf.[0] <- (byte) index;
            ignore(socket.BeginSend(buf,0,1,SocketFlags.None,null,null)) ;  // check, do beginSend(data1,socket1);beginSend(data2,socket1) make data1 be sent first and then data2 for sure?
            if socketStore.ConnectedSockets = socketStore.GetLastMinorSet().GetLength(0) then
                state <- PipeState.Relaying
                let s = new StreamSplitter(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getSegmentSize(),pipeManager.getMinorSocketBufferSize())
                splitterChain.AddToChain(s) 
                splitterChain.Resume()
            ()  
        | _ -> ()
        Monitor.Exit lockobj
    member public this.ThrottleUpSend(byHowManyConnections: int)=
        Monitor.Enter lockobj
        match state with
        |PipeState.Relaying when isMajorSocketOnRelayListenSide= true ->
            throttleUpSize <- byHowManyConnections
            state <- PipeState.ThrottlingUp_PausingSplitter
            splitterChain.Pause(this.SplitterPaused)
            ()
        | _ -> 
            ()
        Monitor.Exit lockobj
//    member public this.ThrottleUpReceive(byHowManyConnections: int)=
//        |PipeState.Relaying when isMajorSocketOnRelayListenSide= true ->
//            throttleUpSize <- byHo
//        | _ -> ()

    member private this.SplitterPaused()=
        // check state, ToDo
        Monitor.Enter lockobj
        match state with
        | PipeState.ThrottlingUp_PausingSplitter when isMajorSocketOnRelayListenSide= true ->
            state <- PipeState.ThrottlingUp_ConnectingFirstConnection
            pipeManager.needAConnection(this)
            ()
        | PipeState.ThrottlingUp_PausingSplitter when isMajorSocketOnRelayListenSide= false ->
            //state <- PipeState.ThrottlingUp_ExchangingInfo
//            let socket = socketStore.MinorSockets.[socketStore.MinorSockets.GetLength(0)-throttleUpSize]
//            state <- PipeState.ThrottlingUp_ConnectingAll
            ()
      //      ignore(socket.BeginSend(BitConverter.GetBytes(splitter.TotalData),0,8,SocketFlags.None,null,null))
        | _ -> ()
        Monitor.Exit lockobj
    member private this.MergerPaused()=
        // check State, ToDo
        Monitor.Enter lockobj
        match state with
        | PipeState.ThrottlingUp_PausingMerger when isMajorSocketOnRelayListenSide = true ->
            state <- PipeState.ThrottlingUp_ConnectingAll
            for i = 1 to (throttleUpSize-1) do
                pipeManager.needAConnection(this)
            ()
        | PipeState.ThrottlingUp_PausingMerger when isMajorSocketOnRelayListenSide = false ->
            
            state <- PipeState.ThrottlingUp_ConnectingAll
            let buf = Array.create 1 (new Byte())
            buf.[0] <- (byte)0
            let socket = socketStore.GetLastMinorSet().[socketStore.ConnectedSockets]
            ignore(socket.BeginSend(buf,0,buf.GetLength(0),SocketFlags.None,null,null)) ;  // check, do beginSend(data1,socket1);beginSend(data2,socket1) make data1 be sent first and then data2 for sure?
            ()
        
        | _ -> ()
        Monitor.Exit lockobj
    member private this.DataReceiveToMajorSocket(result: IAsyncResult)=
        printfn "stub"
        

    member private this.DataSendToMajorSocket(result: IAsyncResult) =
        printfn "stub"

    member private this.DataReceiveToMinorSocket(result: IAsyncResult)=
        Monitor.Enter lockobj
        match state with
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=false->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket =  fst(h)
            let socketIndex = snd(h)
            let received = socket.EndReceive(result)
            if received <> 1 then
                raise(new Exception("couldn't read index"))
            else
                socketStore.AddMinorSocket(socket,((int)(socketIndex.[0])))
                if socketStore.ConnectedSockets = socketStore.GetLastMinorSet().GetLength(0) then // we have received enough connections, now try to connect 
                    state <- PipeState.Connecting
                    pipeManager.needAConnection(this)
            ()
        | PipeState.ThrottlingUp_ReceivingPauseResponse when isMajorSocketOnRelayListenSide = true ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket = fst(h)
            let pa = snd(h)
            let readCount = socket.EndReceive(result)
            if readCount <> pa.GetLength(0) then
                printfn "couldn't read data"
            else
                state <- PipeState.ThrottlingUp_ConnectingAll
                for i = 0 to throttleUpSize-1 do
                    pipeManager.needAConnection(this) 
            ()
        | PipeState.ThrottlingUp_ReadingSyncInfo when isMajorSocketOnRelayListenSide = false -> 
            let h = result.AsyncState :?> (Socket*byte[])
            let socket = fst(h)
            let pa = snd(h)
            let readCount = socket.EndReceive(result)
            if readCount <> pa.GetLength(0) then
                printfn "couldn't read data"
            else
                throttleUpSize <- (int) pa.[5]
                socketStore.AddMinorSet(throttleUpSize)
                socketStore.AddMinorSocket(socket,(int)pa.[0])
                let sync = BitConverter.ToInt32(pa,1)
                state <- PipeState.ThrottlingUp_PausingMerger
                mergerChain.PauseAt(this.MergerPaused,sync)
            ()
        | PipeState.ThrottlingUp_ReadingThrottleCommand when isMajorSocketOnRelayListenSide = false ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket = fst(h)
            let pa = snd(h)
            let readCount = socket.EndReceive(result)
            if readCount <> pa.GetLength(0) then
                printfn "couldn't read data"
            else   
                if pa.[0] = (byte)0 then // throttle UpSend
                    let buf = Array.create 6 (new Byte())
                    state <- PipeState.ThrottlingUp_ReadingSyncInfo
                    ignore(socket.BeginReceive(buf,0,buf.GetLength(0),SocketFlags.None,minorReadCallback,(socket,buf)))
                else // throttleUp Receive
                    printfn "stub"
            ()
        | PipeState.ThrottlingUp_ExchangingInfo when isMajorSocketOnRelayListenSide = false ->
//            let h = result.AsyncState :?> (Socket*byte[])
//            let socket = fst(h)
//            let pa = snd(h)
//            let readCount = socket.EndReceive(result)
//            if readCount <> pa.GetLength(0) then
//                printfn "couldn't read data"
//            else  
//                state <- PipeState.ThrottlingUp_PausingMerger
//                let thu = BitConverter.ToInt32(pa,0)
//                throttleUpSize <- thu
//                let pauseAt = BitConverter.ToUInt64(pa,4)
//                socketStore.GrowMinorArray(thu)
//                let index = socketStore.AddToMinorSockets(socket)
//                printfn "d"
//                merger.Pause(pauseAt,this.MergerPaused)
            ()
        | PipeState.ThrottlingUp_ConnectingAll when isMajorSocketOnRelayListenSide = false ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket =  fst(h)
            let socketIndex = snd(h)
            let received = socket.EndReceive(result)
            if received <> 1 then
                raise(new Exception("couldn't read index"))
            else
                socketStore.AddMinorSocket(socket,((int)(socketIndex.[0])))
                if socketStore.ConnectedSockets = socketStore.GetLastMinorSet().GetLength(0) then // we have received enough connections, now try to connect 
                    state <- PipeState.Relaying
                    let s = new StreamMerger(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getSegmentSize(),pipeManager.getMinorSocketBufferSize())
                    mergerChain.AddToChain(s) 
                    mergerChain.Resume()        
            ()
        | _ -> ()
        Monitor.Exit lockobj
    member private this.DataSendToMinorSocket(result: IAsyncResult)=
        printfn "implement"
            
    
    member private this.ReadSocketIndex(socket:Socket)=
        Monitor.Enter lockobj
        let index = Array.create 1 (new Byte())
        ignore(socket.BeginReceive(index,0,index.GetLength(0),SocketFlags.None,minorReadCallback,(socket,index)))
        Monitor.Exit lockobj

    member private this.InitRelay()=
        Monitor.Enter lockobj
        state <- PipeState.Relaying
        let s = new StreamSplitter(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getSegmentSize(),pipeManager.getMinorSocketBufferSize())
        let m = new StreamMerger(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getSegmentSize(),pipeManager.getMinorSocketBufferSize())
        splitterChain.AddToChain(s)
        mergerChain.AddToChain(m)
        splitterChain.Resume()
        mergerChain.Resume()
        Monitor.Exit lockobj
    member this.Close()= 
        printfn "stub"