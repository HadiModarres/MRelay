// Copyright 2014 Hadi Modarres
// modarres.zadeh@gmail.com
//
// This file is part of MRelay.
// MRelay is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MRelay is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
module EncryptedPipe

open System
open System.Net.Sockets
open System.Threading
open System.Security.Cryptography

let lockobj = new obj()
let mutable totalP = 0
let mutable closedPipes= 0
type EncryptedPipe(receiveStream: NetworkStream,sendStream: NetworkStream,socket1: Socket, socket2: Socket,encryptReceive: bool) as x= 
    let mutable dataDone=0
    let keyIV = Array.create 256 (0uy)
    let mutable key = null
    let mutable iv = null
    let mutable closed = false
       
    do
        x.intr()

    member x.intr()=
        Monitor.Enter lockobj
        totalP <- totalP+1
        Monitor.Exit lockobj
    member x.Close() =      
        if closed = false then
            closed <- true
            Monitor.Enter lockobj 
            
            totalP<- totalP-1
            printfn "total %i" totalP
            Monitor.Exit lockobj

            try
                socket1.Shutdown(SocketShutdown.Both)
                socket2.Shutdown(SocketShutdown.Both)
            with 
            |_ -> ()
            socket1.Close()
            socket2.Close()
            receiveStream.Dispose()
            sendStream.Dispose()               
//    member x.DataDone() =
//            Monitor.Enter lockobj
//            dataDone <- dataDone+1
//            if dataDone = 2 then
//                x.Close()
//            Monitor.Exit lockobj

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

//    member x.ShutdownEncryptDirection() =
//        if encryptReceive = true then
//            socket2.Shutdown(SocketShutdown.Send)
//        else
//            receiveStream.Flush()
//            socket1.Shutdown(SocketShutdown.Send)
//            
//        x.DataDone()
//
//    member x.ShutdownDecryptDirection() =
//
//        if encryptReceive = true then
//            socket1.Shutdown(SocketShutdown.Send)
//        else
//            socket2.Shutdown(SocketShutdown.Send)
//        x.DataDone()


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