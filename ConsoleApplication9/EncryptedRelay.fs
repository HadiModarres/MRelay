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
module EncryptedRelay

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

let SymmetricKeySize = 128

type EncryptedRelay(listenOnPort: int,forwardAddress:IPAddress,forwardPort:int,encryptReceive: bool) as this=
    
    let rsa = new RSACryptoServiceProvider(new CspParameters())
    
    let mutable aes = AesManaged.Create()

    let connectCallback = new AsyncCallback(this.ConnectCallback)

    do
   //     printfn "initiating encrypted relay"
        this.StartListening()
        
    member this.StartListening() =
        let listeningSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
        
        listeningSocket.NoDelay <- true
        let receiveEndpoint = new System.Net.IPEndPoint(IPAddress.Any,listenOnPort)
        
        try
            listeningSocket.Bind(receiveEndpoint)
            listeningSocket.Listen(10000)

            while true do    
                let receivedSocket = listeningSocket.Accept()
                let s = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)
                s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
                s.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
                ignore(s.BeginConnect(forwardAddress,forwardPort,connectCallback,(receivedSocket,s)))
        with
        | e -> printfn "Failed to start encrypted relay: %A" e.Message

    member this.ConnectCallback(result:IAsyncResult) =
        
        let h = result.AsyncState :?> (Socket * Socket)
        let receivedSocket = fst(h)
        let newSocket = snd(h)
       

        let obj3 = new obj()
        let encryptedKeyIVSent (completedTask: Task) (pipe:obj) =
            Monitor.Enter obj3

            let p = pipe :?> EncryptedPipe
            if completedTask.Exception <> null then
                p.Close()
            else
                ignore(new StreamDecryptor(p))
                ignore(new StreamEncryptor(p))


            Monitor.Exit obj3

        let at3 = new Action<Task,obj>(encryptedKeyIVSent)

        let obj2 = new obj()
        let publicKeyReceived (completedTask: Task<string>) ( pipe: obj) =
            Monitor.Enter obj2
            let p = pipe :?> EncryptedPipe
            if completedTask.Exception <> null then
                 p.Close()
            else
                try
                    rsa.FromXmlString(completedTask.Result)
                    let encryptedSymmetricKey = rsa.Encrypt(p.Key,false)
                    let encryptedSymmetricIV = rsa.Encrypt(p.Iv,false)
                    let encKeyIV = Array.append encryptedSymmetricKey encryptedSymmetricIV
                    let stream = p.GetStreamThatNeedsDecryption()
                    ignore(stream.WriteAsync(encKeyIV,0,encKeyIV.GetLength(0)).ContinueWith(at3,p))
                with
                | e -> p.Close() 
            Monitor.Exit obj2

        let obj5 = new obj()
        let keyIVRead(completedTask:Task) (pipe: obj)=
            
            Monitor.Enter obj5
            let p = pipe :?> EncryptedPipe
            if completedTask.Exception <> null then
                p.Close()
            else
                try
                    let key = Array.create 128 (new Byte())
                    let iv =  Array.create 128 (new Byte())
                    Array.blit p.KeyIV 0 key 0 128
                    Array.blit p.KeyIV 128 iv 0 128
                    p.Key <- rsa.Decrypt(key,false)
                    p.Iv <- rsa.Decrypt(iv,false)
                  //  printfn "startingggg %A %A"
                    ignore(new StreamDecryptor(p))

                    ignore(new StreamEncryptor(p))
                with
                | e-> p.Close()
            Monitor.Exit obj5


        let at4 = new Action<Task,obj>(keyIVRead)
        let obj4 = new obj()
        let publicKeySent (completedTask: Task) ( pipe: obj) =
            Monitor.Enter obj4
            let p = pipe :?> EncryptedPipe
            if completedTask.Exception <> null then
                p.Close()
            else
                try
                    ignore(p.GetStreamThatNeedsDecryption().ReadAsync(p.KeyIV,0,256).ContinueWith(at4,p))
                with
                | e-> p.Close()
            Monitor.Exit obj4
        
        let at = new Action<Task<string>,obj>(publicKeyReceived)
        let at2 = new Action<Task,obj>(publicKeySent)

        let obj1 = new obj()
      //  do
      
        Monitor.Enter obj1
        try 
            newSocket.EndConnect(result)
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.NoDelay,true)
            newSocket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.KeepAlive,true)
            let receiveStream = new NetworkStream(receivedSocket)
            let sendStream = new NetworkStream(newSocket)
            let newPipe = new EncryptedPipe(receiveStream,sendStream,receivedSocket,newSocket,encryptReceive)
       //     totalPipes <- (totalPipes + 1)
            try
                if encryptReceive = true then 
                    aes.KeySize <- SymmetricKeySize
                    aes.GenerateKey()
                    aes.GenerateIV()
                    newPipe.Key <- aes.Key
                    newPipe.Iv <- aes.IV 
                    let streamReader = new StreamReader(newPipe.GetStreamThatNeedsDecryption())
                    ignore(streamReader.ReadLineAsync().ContinueWith(at,newPipe))
                else
                    let streamWriter = new StreamWriter(newPipe.GetStreamThatNeedsDecryption())
                    streamWriter.AutoFlush <- true
                    ignore(streamWriter.WriteLineAsync(rsa.ToXmlString(false)).ContinueWith(at2,newPipe))
            with
            | e -> newPipe.Close()                   
                      
                
        with
        | e -> newSocket.Close(); receivedSocket.Close()
        Monitor.Exit obj1
    
