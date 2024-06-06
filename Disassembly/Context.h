typedef char byte;
typedef unsigned char ubyte;
typedef unsigned short ushort;
typedef unsigned int uint;
typedef unsigned long ulong;

typedef unsigned char undefined1;
typedef unsigned int undefined;
typedef unsigned short undefined2;
typedef unsigned int undefined4;
typedef unsigned long undefined8;

typedef long long GPR;
typedef unsigned long long uGPR;

typedef struct
{
    GPR at;
    GPR zero;
    GPR v0;
    GPR v1;
    GPR a0;
    GPR a1;
    GPR a2;
    GPR a3;
    GPR t0;
    GPR t1;
    GPR t2;
    GPR t3;
    GPR t4;
    GPR t5;
    GPR t6;
    GPR t7;
    GPR s0;
    GPR s1;
    GPR s2;
    GPR s3;
    GPR s4;
    GPR s5;
    GPR s6;
    GPR s7;
    GPR t8;
    GPR t9;
    GPR k0;
    GPR k1;
    GPR gp;
    GPR sp;
    GPR fp;
    GPR ra;

    int pc;
    GPR hi;
    GPR lo;
    GPR hi1;
    GPR lo1;
    GPR sa;

    bool InterruptsEnabled;
} RecompContext;

inline void ADD(RecompContext *ctx, int rd, int rs, int rt) { rd = (rs + rt); }
inline void ADDI(RecompContext *ctx, int rs, int rt, int offset) { rs = (rt + offset); }
inline void ADDIU(RecompContext *ctx, uint a, uint b, uint value) { a = b + value; }

inline void AND(RecompContext *ctx, uint rd, uint rs, uint rt) { rd = rs & rt; }
inline void ANDI(RecompContext *ctx, uint rd, uint rs, uint value) { rd = rs & value; }

#define BEQ(ctx, a, b, branch)  \
    if (*(int *)a == *(int *)b) \
        goto branch;
#define BEQL(ctx, a, b, branch) \
    if (*(int *)a == *(int *)b) \
        goto branch;
#define BGEZ(ctx, a, branch) \
    if (*(int *)a >= 0)      \
        goto branch;
#define BGEZAL(ctx, a, branch) \
    if (*(int *)a >= 0)        \
    {                          \
        ctx->ra = ctx->pc + 4; \
        goto branch;           \
    }
#define BGEZALL(ctx, a, branch) \
    if (*(int *)a >= 0)         \
    {                           \
        ctx->ra = ctx->pc + 4;  \
        goto branch;            \
    }
#define BGEZL(ctx, a, branch) BGEZ(ctx, a, branch);

#define BGTZ(ctx, a, branch) \
    if (*(int *)a > 0)       \
        goto branch;
#define BGTZL(ctx, a, branch) BGTZ(ctx, a, branch);

#define BLEZ(ctx, a, branch) \
    if (*(int *)a <= 0)      \
        goto branch;
#define BLEZL(ctx, a, branch) BLEZ(ctx, a, branch)

#define BLTZ(ctx, a, branch) \
    if (*(int *)a < 0)       \
        goto branch;
#define BLZAL(ctx, a, branch)  \
    if (*(int *)a < 0)         \
    {                          \
        ctx->ra = ctx->pc + 4; \
        goto branch;           \
    }
#define BLZALL(ctx, a, branch) BLZAL(ctx, a, branch)
#define BLTZ(ctx, a, branch) \
    if (*(int *)a < 0)       \
        goto branch;

#define BLTZL(ctx, a, branch) BLTZ(ctx, a, branch)
#define BNE(ctx, a, b, branch)  \
    if (*(int *)a != *(int *)b) \
        goto branch;
#define BNEL(ctx, a, b, branch) \
    if (*(int *)a != *(int *)b) \
        goto branch;

#define BREAK ;
#define SYNC ;
#define SYNCL ;

inline void DADD(RecompContext *ctx, long output, long a, long b) { output = a + b; }
inline void DADDI(RecompContext *ctx, long output, long a, long imm) { output = a + imm; }
inline void DADDU(RecompContext *ctx, ulong output, ulong a, ulong b) { output = a + b; }
inline void DADDIU(RecompContext *ctx, ulong output, ulong a, ulong imm) { output = a + imm; }

inline void DIV(RecompContext *ctx, int a, int b)
{
    ctx->hi = a / b;
    ctx->lo = a % b;
}
inline void DIVU(RecompContext *ctx, uint a, uint b)
{
    ctx->hi = a / b;
    ctx->lo = a % b;
}

inline void DSLL(RecompContext *ctx, uint rd, uint rt, uint sa) { rd = rt << sa; }
inline void DSLL32(RecompContext *ctx, uint rd, uint rt, uint sa) { rd = rt << (32 + sa); }
inline void DSLLV(RecompContext *ctx, uint rd, uint rt, uint rs) { rd = rt << rs; }
inline void DSRA(RecompContext *ctx, uint rd, uint rt, uint sa) { rd = rt >> sa; }
inline void DSRA32(RecompContext *ctx, uint rd, uint rt, uint sa) { rd = rt >> (sa + 32); }
inline void DSRAV(RecompContext *ctx, uint rd, uint rt, uint rs) { rd = rt >> rs; }
inline void DSRL(RecompContext *ctx, uint rd, uint rt, uint sa) { rd = rt >> sa; }
inline void DSRL32(RecompContext *ctx, uint rd, uint rt, uint sa) { rd = rt >> (sa + 32); }
inline void DSRLV(RecompContext *ctx, uint rd, uint rt, uint rs) { rd = rt >> rs; }

inline void DSUB(RecompContext *ctx, int rd, int rs, int rt) { rd = rs - rt; }
inline void DSUBU(RecompContext *ctx, uint rd, uint rs, uint rt) { rd = rs - rt; }

inline void LB(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(byte *)(base + offset); }
inline void LBU(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(ubyte *)(base + offset); }

inline void LD(RecompContext *ctx, long rt, short offset, long base) { rt = *(long *)(base + offset); }
inline void LDL(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(long *)(base + offset); } // Not the most accurate but it should work
inline void LDR(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(long *)(base + offset); } // Not the most accurate but it should work

inline void LH(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(short *)(base + offset); }
inline void LHU(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(ushort *)(base + offset); }

inline void LUI(RecompContext *ctx, uint dst, uint value) { dst = (*(uint *)value) << 0x10; }
inline void LW(RecompContext *ctx, GPR rt, int offset, int base) { rt = (base + offset); }
inline void LWL(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(int *)(base + offset); }
inline void LWR(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(int *)(base + offset); }
inline void LWU(RecompContext *ctx, GPR rt, short offset, GPR base) { rt = *(uint *)(base + offset); }

inline void MFHI(RecompContext *ctx, GPR rd) { rd = ctx->hi; }
inline void MFLO(RecompContext *ctx, GPR rd) { rd = ctx->lo; }

inline void MOVN(RecompContext *ctx, GPR rd, GPR rs, GPR rt)
{
    if (rt != 0)
        rd = rs;
}
inline void MOVZ(RecompContext *ctx, GPR rd, GPR rs, GPR rt)
{
    if (rt == 0)
        rd = rs;
}

inline void MTHI(RecompContext *ctx, GPR rs) { ctx->hi = rs; }
inline void MTLO(RecompContext *ctx, GPR rs) { ctx->lo = rs; }

inline void MULT(RecompContext *ctx, GPR rs, GPR rt)
{
    GPR result = rs * rt;
    ctx->hi = result >> 32;
    ctx->lo = result & 0xffffffffu;
}
inline void MULTU(RecompContext *ctx, GPR rs, GPR rt)
{
    GPR result = (uGPR)rs * (uGPR)rt;
    ctx->hi = result >> 32;
    ctx->lo = result & 0xffffffffu;
}

inline void NOR(RecompContext *ctx, GPR rd, GPR rs, GPR rt) { rd = ~(rs | rt); }
inline void OR(RecompContext *ctx, GPR rd, GPR rs, GPR rt) { rd = rs | rt; }
inline void ORI(RecompContext *ctx, GPR rd, GPR rs, short immediate) { rd = rs | immediate; }
#define NOP() ;

inline void SD(RecompContext *ctx, ulong base, ulong value, ulong offset) { *(ulong *)(base + offset) = (ulong)value; }

inline void EI(RecompContext *ctx) { ctx->InterruptsEnabled = true; }
// #define DIV()
