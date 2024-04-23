struct Split
{
    public int globalFileOffset;
    public int length;
    public Symbol targetSymbol;
    public Relocation[] relocations;
    public int[] relocationToInstructionsIndex;
}