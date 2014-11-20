module EncryptedRelayTest

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
open EncryptedRelay
open Microsoft.VisualStudio.TestTools.UnitTesting   

[<TestClass>]
type public EncryptedRelayTest() =
     let a = 2
     let file = @"c:\test\1.exe"
     let encryptedFile = @"c:\test\encrypted.exe"
     let decryptedFile = @"c:\test\decrypted.exe"
     let key = @"1234567887654321"
     let iv = @"1234567887654321"     
     
     let arraysEqual (a:byte[], b:byte[]) =
        let mutable ans = true
        for i=0 to (a.GetLength(0)-1) do
            if a.[i] <> b.[i] then
                ans <- false
        ans

     
     member x.SetupEncryptRelay(port: int,forwardPort: int)=
         let key = Text.Encoding.ASCII.GetBytes(key)
         let iv = Text.Encoding.ASCII.GetBytes(iv)
         let t1 = new Thread( fun () -> ignore(new EncryptedRelay(port,Dns.GetHostAddresses("127.0.0.1").[0],forwardPort,true)))
         t1.Start()
         
         
         
         
         


     member x.SetupEncryptDecryptRelays(port: int, forwardPort: int)=
         let key = Text.Encoding.ASCII.GetBytes(key)
         let iv = Text.Encoding.ASCII.GetBytes(iv)
         let t1 = new Thread( fun () -> ignore(new EncryptedRelay(port,Dns.GetHostAddresses("127.0.0.1").[0],4500,true)))
         let t2 = new Thread( fun () -> ignore(new EncryptedRelay(4500,Dns.GetHostAddresses("127.0.0.1").[0],forwardPort,false)))
         t1.Start()
         t2.Start()

     member x.SendFileToPort()=
        let clientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        let remoteep = new System.Net.IPEndPoint(Dns.GetHostAddresses("127.0.0.1").[0],7000)

        clientSocket.Connect(remoteep)
        printfn "sending file"
        clientSocket.SendFile(file)
        printfn "send done"
        clientSocket.Shutdown(SocketShutdown.Both)
        clientSocket.Close()
        
     member x.ReceiveFile(port: int, filePath: string)=
        let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        let remoteep = new System.Net.IPEndPoint(IPAddress.Any,port)
        listeningSocket.Bind(remoteep)
        listeningSocket.Listen(3)
        let newSocket = listeningSocket.Accept()


        let buf = Array.create (64*1024) (new Byte())
       // let is = File.Open(Path.GetFullPath(@"c:\test\1.exe"),FileMode.Open,FileAccess.Read)
   //     use fs = File.Create(@"c:\test\output.exe")
        let fs = File.Open(Path.GetFullPath(filePath),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)
   //     let fs2 = File.Open(Path.GetFullPath(@"c:\test\output2.jar"),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)
        
        let rec readMore()=
       //     printfn "readmore"
            let readCount = newSocket.Receive(buf)
            if readCount > 0 then
                fs.Write(buf,0,readCount)
                readMore()
            else 
                printfn "no more data available,socket 1"

       

        readMore()
        printfn "read done"
        newSocket.Close()
        printfn "socket closed"
        fs.Flush()        
        printfn "file flushed"
        fs.Dispose()
        fs.Close()
        printfn "file closed"

//     member private x.checkFileMDR(file1: string, file2: string)=
//        
//        let f1 = File.Open(Path.GetFullPath(file1),FileMode.Open,FileAccess.Read)
//        let f2 = File.Open(Path.GetFullPath(file2),FileMode.Open,FileAccess.Read,FileShare.ReadWrite)
//
//        let md5Result= MD5.Create().ComputeHash(f1)
//        let md5Result2 =  MD5.Create().ComputeHash(f2)
//        f1.Close()
//        f2.Close()
//        if arraysEqual(md5Result,md5Result2) then
//            true
//        else 
//            false



//     member x.TestEncrypt() =
//            if File.Exists(encryptedFile) then
//                 File.Delete(encryptedFile)
//            let f = File.Create(encryptedFile,1024*1024,FileOptions.None)
//            f.Close()
//
//            x.SetupEncryptRelay(4000,5000)
//            let t = new Thread(x.SendFileToPort)
//            t.Start()
//            x.ReceiveFile(5000,encryptedFile)
//            printfn "file received."
//            t.Join()
//
//            if x.checkFileMDR(file,encryptedFile) = true then
//                printfn "test1 failed"
//            else
//                printfn "test1 successful"


     [<TestMethod>]
     member x.TestEncryptDecrypt()=
            if File.Exists(decryptedFile) then
                 File.Delete(decryptedFile)
            let f = File.Create(decryptedFile,1024*1024,FileOptions.None)
            f.Close()

            x.SetupEncryptDecryptRelays(7000,8000)
            let t = new Thread(x.SendFileToPort)
            t.Start()
            x.ReceiveFile(8000,decryptedFile)
            printfn "file received."
            t.Join()

//            if x.checkFileMDR(file,decryptedFile) = false then
//                printfn "test2 failed"
//            else
//                printfn "test2 successful"
            Assert.AreEqual(x.GetFileMDR(file),x.GetFileMDR(decryptedFile),false)
     member private x.GetFileMDR(fileName: string)=
        let f1 = File.Open(Path.GetFullPath(fileName),FileMode.Open,FileAccess.Read,FileShare.ReadWrite)
        let md5Result= MD5.Create().ComputeHash(f1)
        let md5String = System.Text.Encoding.ASCII.GetString(md5Result)
        f1.Flush()
        f1.Dispose()
        f1.Close()
        md5String
    
    


