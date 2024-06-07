public interface ISectionHeader
{
    public enum SH_Type { NULL, PROGBITS, SymbolTable, StringTable, RelocationAddend, Hash, Dynamic, Note, NoBits, Relocatable, SHLIB, DynamicSymbol };
    
    public string Name { get; set; }
    public int MemoryAddress { get; set; }
    public int FileOffset { get; set; }
    public int Length { get; set; }
    public SH_Type Type{ get; set; }


    // Metadata
    public int SectionIndex { get; set; }
    public byte[] Data { get; set; }
}