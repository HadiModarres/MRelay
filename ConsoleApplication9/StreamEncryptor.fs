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
module StreamEncryptor

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open System.Security.Cryptography
open System.Threading.Tasks
open EncryptedPipe
open System.Net.Security
open phelix



type StreamEncryptor(pipe: EncryptedPipe) as this = 
    
    
    let phelix = new Phelix(false,pipe.GetKeyCopy(),0,pipe.GetKeyCopy().GetLength(0),pipe.GetIVCopy(),0,pipe.GetIVCopy().GetLength(0))
    let receiveBuffer = Array.create (1024) (new Byte())
    
    let at3  = new Action<Task>(this.bytesSent)
    let at = new Action<Task>(this.closeSockets)
    let at2 = new Action<Task<int>>(this.bytesRead)
    
        
    do
      //  printfn "initiating streamencryptor"
        totalPipes <- totalPipes+1
        try
            ignore(pipe.GetStreamThatNeedsEncryption().ReadAsync(receiveBuffer,0,receiveBuffer.GetLength(0)).ContinueWith(at2))
        with
        | e-> pipe.Close()
    member this.bytesRead(completedTask: Task<int>)=
        if completedTask.Exception <> null then
            pipe.Close()    
        else
            if completedTask.Result = 0 then
                this.closeSockets(completedTask)
            else
                let toBeEncrypted = Array.create completedTask.Result (new Byte())
                Array.blit receiveBuffer 0 toBeEncrypted 0 completedTask.Result
                let encrypted = Array.map phelix.next toBeEncrypted
                try
                    ignore(pipe.GetStreamThatNeedsDecryption().WriteAsync(encrypted,0,encrypted.GetLength(0)).ContinueWith(at3))
                with
                | e-> pipe.Close()
    member this.closeSockets(completedTask: Task)=
        if (completedTask.Exception <> null) then
            pipe.Close()
        else
            pipe.ShutdownEncryptDirection()

    member this.bytesSent(completedTask: Task)=
        if completedTask.Exception<>null then
            pipe.Close()
        else
            try
                ignore(pipe.GetStreamThatNeedsEncryption().ReadAsync(receiveBuffer,0,receiveBuffer.GetLength(0)).ContinueWith(at2))
            with
            | e-> pipe.Close()
