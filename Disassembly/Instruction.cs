
using System.Numerics;

public class Register
{
    public static bool WriteRegisterName = true;

    public Register(uint value)
    {
        register = (_Register)value;
    }
    public Register(int value)
    {
        register = (_Register)value;
    }

    public enum _Register
    {
        zero = 0,
        at = 1,
        v0 = 2,
        v1 = 3,
        a0 = 4,
        a1 = 5,
        a2 = 6,
        a3 = 7,
        t0 = 8,
        t1 = 9,
        t2 = 10,
        t3 = 11,
        t4 = 12,
        t5 = 13,
        t6 = 14,
        t7 = 15,
        s0 = 16,
        s1 = 17,
        s2 = 18,
        s3 = 19,
        s4 = 20,
        s5 = 21,
        s6 = 22,
        s7 = 23,
        t8 = 24,
        t9 = 25,
        k0 = 26,
        k1 = 27,
        gp = 28,
        sp = 29,
        fp = 30,
        ra = 31,
    }

    private _Register register;

    public static implicit operator int(Register r)
    {
        return (int)r.register;
    }

    public static implicit operator Register(int r)
    {
        return new Register(r);
    }
    public static implicit operator Register(uint r)
    {
        return new Register((int)r);
    }

    public static bool operator ==(Register r1, Register r2)
    {
        return r1.register.Equals(r2.register);
    }


    public static bool operator !=(Register r1, Register r2)
    {
        return !r1.register.Equals(r2.register);
    }


    public override string ToString()
    {
        if (WriteRegisterName)
            return register.ToString();
        else return ((int)register).ToString();
    }

    public override bool Equals(object? obj)
    {
        if (obj is Register r2)
            return r2.register.Equals(register);

        return false;
    }
}

public abstract class Instruction
{
    public string Name = string.Empty;
    public uint Data { get; set; }

    public virtual void Read(uint data, int opcode)
    {
        Data = data;
    }

    public virtual string ToCMacro(string branch = "")
    {
        return $"{Name.ToUpper()}(); // Unimplemented";
    }


    protected static string OpcodeToName(int opcode)
    {
        switch (opcode)
        {
            default:
                Console.WriteLine($"Unknown opcode 0x{opcode:X} / {opcode >> 3 & 0x7:b} {opcode & 7:b}b");
                return $"UNKNOWN-OPCODE '0x{opcode:X}'";
            case 0x0: return "WRONG REGISTER TYPE";
            case 0x1: return "UNDEFINED REGIMM";
            case 0x2: return "j";
            case 0x3: return "jal";
            case 0x4: return "beq";
            case 0x5: return "bne";
            case 0x6: return "blez";
            case 0x7: return "bgtz";
            case 0x8: return "addi";
            case 0x9: return "addiu";
            case 0xA: return "slti";
            case 0xB: return "sltiu";
            case 0xC: return "andi";
            case 0xD: return "ori";
            case 0xE: return "xori";
            case 0xF: return "lui";
            case 0x10: return "mfc0";
            // case 0x11: return GetFPUOpcodeToName(data);
            case 0x11: return "Unknown FPU OPCODE";
            case 0x14: return "beql";
            case 0x15: return "bnel";
            case 0x16: return "blezl";
            case 0x17: return "bgtzl";
            case 0x18: return "daddi";
            case 0x19: return "daddiu";
            case 0x1a: return "ldl";
            case 0x1b: return "ldr";
            // case 0x1c: return "ldl";
            case 0x1e: return "lq";
            case 0x1f: return "sq";
            case 0x20: return "lb";
            case 0x21: return "lh";
            case 0x22: return "lwl";
            case 0x23: return "lw";
            case 0x24: return "lbu";
            case 0x25: return "lhu";
            case 0x26: return "lwr";
            case 0x27: return "lwu";
            case 0x28: return "sb";
            case 0x29: return "sh";
            case 0x2a: return "swl";
            case 0x2b: return "sw";
            case 0x2c: return "sdl";
            case 0x2d: return "sdr";
            case 0x2e: return "swr";
            case 0x2f: return "cache";
            case 0x31: return "lwc1";
            case 0x33: return "pref";
            case 0x37: return "ld";
            case 0x3f: return "sd";
        }
    }

    public abstract string ToString(string symbol);

}