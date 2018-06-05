namespace bitprim.insight.DTOs
{
    public class GetTxsForMultipleAddressesRequest
    {
        public string addrs { get; set; }
        public int from { get; set; } = 0;
        public int to { get; set; } = 10;
        public int noAsm { get; set; } = 1;
        public int noScriptSig { get; set; } = 1;
        public int noSpend { get; set; } = 1;
    }
}