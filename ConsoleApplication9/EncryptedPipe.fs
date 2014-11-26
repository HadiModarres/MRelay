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
module EncryptedPipe

open System
open System.Net.Sockets
open System.Threading
open System.Security.Cryptography

let mutable totalPipes = 0
let mutable closedPipes= 0
type EncryptedPipe(receiveStream: NetworkStream,sendStream: NetworkStream,socket1: Socket, socket2: Socket,encryptReceive: bool) = 
    let mutable dataDone=0
    let lockobj = new obj()
//    let mutable rsa = new RSACryptoServiceProvider(new CspParameters())
    let keyIV = Array.create 256 (0uy)
//    let mutable aes = AesManaged.Create()
    let mutable key = null
    let mutable iv = null
    let mutable closed = false
       

    
    member x.Close() =       
        if closed = false then
            closed <- true
            closedPipes <- closedPipes+1
            printfn "total closed encrypted pipes: %i" closedPipes
            socket1.Close()
            socket2.Close()
            receiveStream.Dispose()
            sendStream.Dispose()               
    member x.DataDone() =
            Monitor.Enter lockobj
            dataDone <- dataDone+1
            if dataDone = 2 then
                printfn "both directions done"
                x.Close()
            Monitor.Exit lockobj

    member x.GetStreamThatNeedsEncryption() =
        if encryptReceive = true then
            receiveStream
        else 
            sendStream
        
    member x.GetStreamThatNeedsDecryption() =
        if encryptReceive = true then
            sendStream
        else 
            receiveStream

    member x.ShutdownEncryptDirection() =
        printfn "shutting down encrypt direction"
        if encryptReceive = true then
         //   socket1.Shutdown(SocketShutdown.Receive)
            socket2.Shutdown(SocketShutdown.Send)
        else
            printfn "here"
            receiveStream.Flush()
        //    socket2.Shutdown(SocketShutdown.Receive)
            socket1.Shutdown(SocketShutdown.Send)
            
            printfn "encrypt shutdown complete"
        x.DataDone()

    member x.ShutdownDecryptDirection() =
        printfn "shutting down decrypt direction"

        if encryptReceive = true then
            printfn "enc rec"
          //  socket2.Shutdown(SocketShutdown.Receive)
            socket1.Shutdown(SocketShutdown.Send)
        else
            printfn "!enc rec"
        //    socket1.Shutdown(SocketShutdown.Receive)
            socket2.Shutdown(SocketShutdown.Send)
        x.DataDone()


    member x.KeyIV
        with get() = keyIV

    member x.IsServer
        with get() = encryptReceive

    member x.Key
        with get() = key
        and set(k: byte[])=  key <- k 
    member x.Iv
        with get() = iv
        and set(i: byte[])=  iv <- i 
    member x.GetKeyCopy()=
        let copy = Array.create (key.GetLength(0)) (new Byte())
        Array.blit key 0 copy 0 (key.GetLength(0))
        copy
    member x.GetIVCopy()=
        let copy = Array.create (iv.GetLength(0)) (new Byte())
        Array.blit iv 0 copy 0 (iv.GetLength(0))
        copy