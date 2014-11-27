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
for i= 0 to 400000 do   
   aes.GenerateKey()
   aes.GenerateIV()

printfn "done"




