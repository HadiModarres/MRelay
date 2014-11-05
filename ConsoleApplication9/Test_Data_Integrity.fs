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
module Test_Data_Integrity

open System
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


type public testSuite() = 
    let threads = new ArrayList()
    let arraysEqual (a:byte[], b:byte[]) =
        let mutable ans = true
        for i=0 to (a.GetLength(0)-1) do
            if a.[i] <> b.[i] then
                ans <- false
        ans


    member x.data_integrity_test() = 

        
        if File.Exists(@"c:\test\output.exe") then
            File.Delete(@"c:\test\output.exe")
        let f = File.Create(@"c:\test\output.exe",1024*1024,FileOptions.None)
        f.Close()

        if File.Exists(@"c:\test\output2.jar") then
            File.Delete(@"c:\test\output2.jar")
        let f = File.Create(@"c:\test\output2.jar",1024*1024,FileOptions.None)
        f.Close()

        let t1 = new Thread( fun () -> ignore(new Relay(4000,1,Dns.GetHostAddresses("127.0.0.1").[0],5000,1,1024,2048,false,false)))
        let t2 = new Thread( fun () -> ignore(new Relay(5000,1,Dns.GetHostAddresses("127.0.0.1").[0],6000,1,1024,2048,false,false)))
        
        let t3 = new Thread (x.StartClient)
        let t4 = new Thread(x.StartClient2)

        let t5  = new Thread(x.StartServer)

        t5.Start()        
        t1.Start() 
        t2.Start()
        t3.Start()
        Thread.Sleep(1000)
        t4.Start()
        
        t5.Join()
        t3.Join()
        t4.Join()
 
        if x.checkFileMDR(@"c:\test\1.exe",@"c:\test\output.exe") then
            printfn "Test1 Completed!"
        else
            printfn "Test1 Failed"

        if x.checkFileMDR(@"c:\test\rt.jar",@"c:\test\output2.jar") then
            printfn "Test2 Completed!"
        else
            printfn "Test2 Failed"
            
        
        Console.Read()
    
    member private x.checkFileMDR(file1: string, file2: string)=
        
     //   let f1s = new FileStream("c:\test\output.exe")
        let f1 = File.Open(Path.GetFullPath(file1),FileMode.Open,FileAccess.Read)
        let f2 = File.Open(Path.GetFullPath(file2),FileMode.Open,FileAccess.Read,FileShare.ReadWrite)

        let md5Result= MD5.Create().ComputeHash(f1)
        let md5Result2 =  MD5.Create().ComputeHash(f2)
      //  printfn "%A   %A" md5Result md5Result2
        f1.Close()
        f2.Close()
        if arraysEqual(md5Result,md5Result2) then
            true
        else 
            false
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
        listeningSocket.Bind(remoteep)
        listeningSocket.Listen(3)
        let newSocket = listeningSocket.Accept()
        let newSocket2 = listeningSocket.Accept()

        printfn "accepted two connections .."
        let buf = Array.create 8192 (new Byte())
        let buf2 = Array.create 8192 (new Byte())
       // let is = File.Open(Path.GetFullPath(@"c:\test\1.exe"),FileMode.Open,FileAccess.Read)
   //     use fs = File.Create(@"c:\test\output.exe")
        let fs = File.Open(Path.GetFullPath(@"c:\test\output.exe"),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)
        let fs2 = File.Open(Path.GetFullPath(@"c:\test\output2.jar"),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)
        
        let rec readMore()=
       //     printfn "readmore"
            let readCount = newSocket.Receive(buf)
            if readCount > 0 then
                fs.Write(buf,0,readCount)
                readMore()
            else 
                printfn "no more data available,socket 1"

        let rec readMore2()=
         //   printfn "readmore2"
            let readCount = newSocket2.Receive(buf2)
            if readCount > 0 then
                fs2.Write(buf2,0,readCount)
                readMore2()
            else 
                printfn "no more data available,socket 1"

        let t1 = new Thread(readMore)
        let t2 = new Thread(readMore2)

        t1.Start()
        t2.Start()

        t1.Join()
        t2.Join()
        printfn "flushing"
        newSocket.Close()
        newSocket2.Close()
        fs.Flush()
        fs2.Flush()
        
        printfn "disposing"
        fs.Dispose()
        fs2.Dispose()
        printfn "closing"
        fs.Close()
        fs2.Close()