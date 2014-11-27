module IPipeManager

open IDataPipe


[<AllowNullLiteral>]
type IPipeManager= 
    abstract needAConnection: obj -> unit 
    abstract getSegmentSize: unit -> int
    abstract getMinorSocketBufferSize: unit -> int
    abstract getDynamicSocketBufferSize: unit-> int
    abstract getDynamicSegmentSize :unit-> int
    abstract dataTransferIsAboutToBegin: IDataPipe -> unit
    abstract pipeDone: obj -> unit
