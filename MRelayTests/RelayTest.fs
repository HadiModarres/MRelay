namespace UnitTestProject1

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
[<TestClass>]
type RelayTest() =   
    let getAvilablePort() =
        let l = new TcpListener(IPAddress.Loopback,0)
        l.Start()
        let p = (l.LocalEndpoint :?> IPEndPoint).Port
        l.Stop()
        p
    [<TestMethod>]
    member x.TestFileTransferMultiTcp()=
//        x.TestFileTransfer(1,1024,2048)
//        x.TestFileTransfer(1,4000,8000)
//        x.TestFileTransfer(1,64*1024,64*1024)
        x.TestFileTransfer(5,10000,10000,false)
        x.TestFileTransfer(60,1024,1024,false)

    [<TestMethod>]
    member x.TestFileTransferSingleTcp()=
        x.TestFileTransfer(1,1024,2048,false)
        x.TestFileTransfer(1,4000,8000,false)
        x.TestFileTransfer(1,64*1024,64*1024,false)
    
    [<TestMethod>]
    member x.TestSingleTcpFakeHeader()=
        x.TestFileTransfer(1,100,900,true)

    [<TestMethod>]
    member x.TestMultiTcpFakeHeader()=
        x.TestFileTransfer(6,16*1024,64*1024,true)

      
    member x.TestFileTransfer (tcpCount: int, segmentSize: int, minorSocketBufferSize: int,fake: bool) = 
        let newp = getAvilablePort()
        let t1 = new Thread( fun () -> ignore(new Relay(4000,1,Dns.GetHostAddresses("127.0.0.1").[0],5000,tcpCount,segmentSize,minorSocketBufferSize,segmentSize,minorSocketBufferSize,true,true)))
        let t2 = new Thread( fun () -> ignore(new Relay(5000,tcpCount,Dns.GetHostAddresses("127.0.0.1").[0],6000,1,segmentSize,minorSocketBufferSize,segmentSize,minorSocketBufferSize,true,false)))
        
        t1.IsBackground <- true
        t2.IsBackground <- true
        let t3 = new Thread (x.StartClient)
        //let t4 = new Thread (x.StartClient2)

        let t5  = new Thread(x.StartServer)

        do
            ignore(x.CheckFiles())
            t5.Start()        
            t1.Start() 
            t2.Start()
            t3.Start()

            Thread.Sleep(500)    
                   
//            t4.Start()
        
            //Thread.Sleep(3000)

            t5.Join()
            t3.Join()
//            t4.Join()

          //  t1.Abort()
          //  t2.Abort()
       //     printfn "relay aborted"
//            t1.Join()
//            t2.Join()

            let s3 = x.GetFileMDR(@"c:\test\1.exe")
            let s4 = x.GetFileMDR(@"c:\test\output.exe")
            Assert.AreEqual(s3,s4,true)
//            let s1 = x.GetFileMDR(@"c:\test\rt.jar")
//            let s2 = x.GetFileMDR(@"c:\test\output2.jar")
//            Assert.AreEqual(s1,s2,true)
//        

    member x.CheckFiles()=
        if File.Exists(@"c:\test\output.exe") then
            File.Delete(@"c:\test\output.exe")
        let f1 = File.Create(@"c:\test\output.exe",1024*1024,FileOptions.None)
        f1.Flush()
        f1.Dispose()
        f1.Close()
        
        if File.Exists(@"c:\test\output2.jar") then
            File.Delete(@"c:\test\output2.jar")
        let f2 = File.Create(@"c:\test\output2.jar",1024*1024,FileOptions.None)
        f2.Flush()
        f2.Dispose()
        f2.Close()

    member private x.GetFileMDR(fileName: string)=
        let f1 = File.Open(Path.GetFullPath(fileName),FileMode.Open,FileAccess.Read,FileShare.ReadWrite)
        let md5Result= MD5.Create().ComputeHash(f1)
        let md5String = System.Text.Encoding.ASCII.GetString(md5Result)
        f1.Flush()
        f1.Dispose()
        f1.Close()
        md5String
    
    
        
    member private x.StartClient() = 
        let clientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        let remoteep = new System.Net.IPEndPoint(Dns.GetHostAddresses("127.0.0.1").[0],4000)

        clientSocket.Connect(remoteep)
      //  Thread.Sleep(7000)
        printfn "sending file"
        clientSocket.SendFile(@"c:\test\1.exe")
        clientSocket.Shutdown(SocketShutdown.Both)
        clientSocket.Close()
                
    member private x.StartClient2()=
        let clientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        let remoteep = new System.Net.IPEndPoint(Dns.GetHostAddresses("127.0.0.1").[0],4000)
        clientSocket.Connect(remoteep)
        clientSocket.SendFile(@"c:\test\rt.jar")
        clientSocket.Shutdown(SocketShutdown.Both)
        clientSocket.Close()
        
       
        
    member private x.StartServer()= 
        let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        let remoteep = new System.Net.IPEndPoint(IPAddress.Any,6000)
        listeningSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true)
        listeningSocket.Bind(remoteep)
        listeningSocket.Listen(3)
        let newSocket = listeningSocket.Accept()
//        let newSocket2 = listeningSocket.Accept()

        listeningSocket.Dispose()
        listeningSocket.Close()
        printfn "accepted two connections .."
        let buf = Array.create 8192 (new Byte())
//        let buf2 = Array.create 8192 (new Byte())
       // let is = File.Open(Path.GetFullPath(@"c:\test\1.exe"),FileMode.Open,FileAccess.Read)
   //     use fs = File.Create(@"c:\test\output.exe")
        let fs = File.Open(Path.GetFullPath(@"c:\test\output.exe"),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)
//        let fs2 = File.Open(Path.GetFullPath(@"c:\test\output2.jar"),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)
        
        let rec readMore()=
       //     printfn "readmore"
            let readCount = newSocket.Receive(buf)
            if readCount > 0 then
                fs.Write(buf,0,readCount)
                readMore()
            else 
                printfn "no more data available,socket 1"

//        let rec readMore2()=
//         //   printfn "readmore2"
//            let readCount = newSocket2.Receive(buf2)
//            if readCount > 0 then
//                fs2.Write(buf2,0,readCount)
//                readMore2()
//            else 
//                printfn "no more data available,socket 1"

        let t1 = new Thread(readMore)
//        let t2 = new Thread(readMore2)

        t1.Start()
//        t2.Start()

        t1.Join()
//        t2.Join()
        
        printfn "flushing"
        newSocket.Shutdown(SocketShutdown.Both)
        newSocket.Close()
//        newSocket2.Close()
        fs.Flush()
//        fs2.Flush()
        
        printfn "disposing"
        fs.Dispose()
//        fs2.Dispose()
        printfn "closing"
        fs.Close()
//        fs2.Close()



