module Server
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open System.Security.Cryptography
open System.Resources
open System.IO
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open SocketStore
open Relay
open DataDelegate

  
type DataServer(listenOn:int, size: int,deleg: IDataDelegate) as x=
    let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
   
    do
        let remoteep = new System.Net.IPEndPoint(IPAddress.Any,listenOn)
        listeningSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true)
        listeningSocket.Bind(remoteep)
        listeningSocket.Listen(30)
      
    
        
    let recCallback = new AsyncCallback(x.ReceiveCallback)
    let buffer = Array.create size 0uy

    member x.StartListening()=
        while true do
            let newSocket = listeningSocket.Accept()
            printfn "what"
            printfn "what"
            printfn "what"
            printfn "what"
            printfn "what"

            x.ReceiveData(newSocket)

    member x.ReceiveData(sock: Socket)=
        
        ignore(sock.BeginReceive(buffer,0,buffer.GetLength(0),SocketFlags.None,recCallback,(sock,0)))
        

    member x.ReceiveCallback(result: IAsyncResult)=
        let h = result.AsyncState :?> (Socket*int)

        let sock = fst(h)
        let index = snd(h)
        let count = sock.EndReceive(result)
        if count = 0 then
       //     Assert.Fail("didn't receive whole data")
           let hash = MD5.Create().ComputeHash(buffer)
           deleg.ReceivedDataHash(hash)
           sock.Shutdown(SocketShutdown.Both)
           sock.Close()
        else
            
            ignore(sock.BeginReceive(buffer,count+index,buffer.GetLength(0)-(count+index),SocketFlags.None,recCallback,(sock,count+index)))

      
        