module HttpHeaderFactory

open System


type HeaderFactory()=

   
    static member GuidSubString(guid:string,offset: int, length: int)=
        let parts = guid.Split([|"-"|],StringSplitOptions.None)
        let a = Array.sub parts offset length 
        (Array.fold (fun (s:String) (s2:String)->s+"-"+s2) "" a).Substring(1)

    static member GuidToHttp(guid: string)=
   // printfn "guid: %A" guid
        
        let h = "GET /"+HeaderFactory.GuidSubString(guid,0,5)+"/"+HeaderFactory.GuidSubString(guid,5,5)+".iso HTTP/1.1\r\nFrom: "+HeaderFactory.GuidSubString(guid,10,6)+"@gmail.com\r\nConnection: keep-alive\r\nUser-Agent: Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.132 Safari/537.36\r\naccept-encoding: gzip,deflate\r\nAccept-Language: en-US,en;q=0.8\r\n\r\n"
        let b = h.Length
        System.Text.Encoding.ASCII.GetBytes(h)

    static member GuidToHttpTest(guid: string)=
        let h = guid
        System.Text.Encoding.ASCII.GetBytes(h)

    static member GuidtoPost(guid: string)=
        let h = "POST /"+guid+".iso HTTP/1.0\r\ncontent-type:application/octet-stream\r\nhost: https://importexport.amazonaws.com\r\ncontent-length:20756565\r\n\r\n"
        System.Text.Encoding.ASCII.GetBytes(h)
    static member HttpToGuid(http: string)=
        let c = http.Split([|" "; "/"; "@"; "."|],StringSplitOptions.None)
        c.[2]+"-"+c.[3]+"-"+c.[8]

    static member HttpToGuidTest(http: string)=
        http

    static member GetResponseHeader()=
        let h="HTTP/1.1 200 OK\r\nDate: Fri, 31 Dec 2014 23:59:59 GMT\r\nServer: Apache/2.2.23 (CentOS)\r\nLast-Modified: Wed, 18 Feb 2015 20:12:10 GMT\r\nContent-Length: 1044381696\r\nConnection: close\r\nContent-Type: application/octet-stream\r\n\r\n"
        System.Text.Encoding.ASCII.GetBytes(h)
    
    static member GetResponseHeaderTest()=
        let h= "dummy"
        System.Text.Encoding.ASCII.GetBytes(h)
    
    static member GetRequestSize()=
        297
    static member GetRequestSizeTest()=
        47
    static member getResponseSize()=
        HeaderFactory.GetResponseHeader().GetLength(0)
    
    static member getResponseSizeTest()=
        HeaderFactory.GetResponseHeaderTest().GetLength(0)
    

    static member getPostSize()=
        30

    static member IsPost(header: byte[])=
        if header.[0]=80uy then
            true
        else
            false
       
    static member PostToGuid(post: string)=
        let c = post.Split([|" "; "/";".iso"|],StringSplitOptions.None)
        let s= c.[2]
        s
    
//let gui = Guid.NewGuid().ToByteArray()
//printfn "%A" (BitConverter.ToString(gui))
//printfn "%A %i" (HeaderFactory.GuidToHttp(BitConverter.ToString(gui))) (HeaderFactory.GuidToHttp(BitConverter.ToString(gui))).Length
