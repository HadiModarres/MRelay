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
module RSAPerformanceTest

open System
open System.Security.Cryptography
open System.Security



let mutable aes = AesManaged.Create()
aes.KeySize <- 256
//let arr = Array.create 2048 (new Byte())
//let arr2 = rsa.Encrypt(aes.Key,false)
open System
let GuidSubString(guid:byte[],offset: int, length: int)=
    BitConverter.ToString((Array.sub guid offset length))
let GuidToHttp(guid: byte[])=
   // printfn "guid: %A" guid
    "GET /"+GuidSubString(guid,0,5)+"/"+GuidSubString(guid,5,5)+".iso HTTP/1.0\r\nFrom: "+GuidSubString(guid,10,6)+"@gmail.com\r\nUser-Agent: Mozilla/13.0\r\n\r\n"


let HttpToGuid(http: string)=
    let c = http.Split([|" "; "/"; "@"; "."|],StringSplitOptions.None)
    c.[2]+"-"+c.[3]+"-"+c.[8]

let g = Guid.NewGuid().ToByteArray()
printfn "guid: %A" g
let k = GuidToHttp(g)
//printfn "%A" (k.Split([|" "; "/"; "@"; "."|],StringSplitOptions.None))
printfn "%A" k
printfn "%A" (HttpToGuid(k))
let z = System.Text.Encoding.ASCII.GetBytes(k)
printfn "header size: %i" (z.GetLength(0))

let getResponseHeader()=
    "HTTP/1.0 200 OK\r\nDate: Fri, 31 Dec 2014 23:59:59 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 4294962296\r\n\r\n"


//let h = Array.create 2 0uy
//h.[0] <- 56uy
//h.[1] <-230uy
//printfn "%A" (BitConverter.ToString(h))
//printfn "GET /path/file.iso HTTP/1.0\r\nFrom: someuser@gmail.com\r\nUser-Agent: Mozilla/13.0\r\n\r\n"


    

for i= 0 to 400000 do   
   aes.GenerateKey()
   aes.GenerateIV()

printfn "done"




