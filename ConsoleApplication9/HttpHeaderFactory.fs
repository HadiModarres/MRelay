module HttpHeaderFactory

open System

type HeaderFactory()=
    static member GuidSubString(guid:string,offset: int, length: int)=
        let parts = guid.Split([|"-"|],StringSplitOptions.None)
        let a = Array.sub parts offset length 
        (Array.fold (fun (s:String) (s2:String)->s+"-"+s2) "" a).Substring(1)

    static member GuidToHttp(guid: string)=
   // printfn "guid: %A" guid
        
        let h = "GET /"+HeaderFactory.GuidSubString(guid,0,5)+"/"+HeaderFactory.GuidSubString(guid,5,5)+".iso HTTP/1.0\r\nFrom: "+HeaderFactory.GuidSubString(guid,10,6)+"@gmail.com\r\nUser-Agent: Mozilla/13.0\r\n\r\n"
        System.Text.Encoding.ASCII.GetBytes(h)

    static member HttpToGuid(http: string)=
        let c = http.Split([|" "; "/"; "@"; "."|],StringSplitOptions.None)
        c.[2]+"-"+c.[3]+"-"+c.[8]

    static member GetResponseHeader()=
        let h="HTTP/1.0 200 OK\r\nDate: Fri, 31 Dec 2014 23:59:59 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 4294962296\r\n\r\n"
        System.Text.Encoding.ASCII.GetBytes(h)
    static member GetRequestSize()=
        112
    static member getResponseSize()=
        HeaderFactory.GetResponseHeader().GetLength(0)
    
//let gui = Guid.NewGuid().ToByteArray()
//printfn "%A" (BitConverter.ToString(gui))
//printfn "%A %i" (HeaderFactory.GuidToHttp(BitConverter.ToString(gui))) (HeaderFactory.GuidToHttp(BitConverter.ToString(gui))).Length
