/**
 * Ported from Tom Pittman's:
 * http://www.ittybittycomputers.com/IttyBitty/TinyBasic/TinyBasic.cby Tom Pittman.
 * Port by Mohan Embar, http://www.thisiscool.com/
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace com.thisiscool.tinybasic
{
    public class TinyBasic
    {
        public const int CoreTop = 65536; // Core size
        public const int UserProg = 32; // Core address of front of Basic program
        public const int EndUser = 34; // Core address of end of stack/user space
        public const int EndProg = 36; // Core address of end of Basic program
        public const int GoStkTop = 38; // Core address of Gosub stack top
        public const int LinoCore = 40; // Core address of "Current BASIC line number"
        public const int ILPCcore = 42; // Core address of "IL Program Counter"
        public const int BPcore = 44; // Core address of "Basic Pointer"
        public const int SvPtCore = 46; // Core address of "Saved Pointer"
        public const int InLine = 48; // Core address of input line
        public const int ExpnStk = 128; // Core address of expression stack (empty)
        public const int TabHere = 191; // Core address of output line size, for tabs
        public const int WachPoint = 255; // Core address of debug watchpoint USR
        public const int ColdGo = 256; // Core address of nominal restart USR
        public const int WarmGo = 259; // Core address of nominal warm start USR
        public const int InchSub = 262; // Core address of nominal char input USR
        public const int OutchSub = 265; // Core address of nominal char output USR
        public const int BreakSub = 268; // Core address of nominal break test USR
        public const int DumpSub = 273; // Core address of debug core dump USR
        public const int PeekSub = 276; // Core address of nominal byte peek USR
        public const int Peek2Sub = 277; // Core address of nominal 2-byte peek USR
        public const int PokeSub = 280; // Core address of nominal byte poke USR
        public const int TrLogSub = 283; // Core address of debug trace log USR
        public const int BScode = 271; // Core address of backspace code
        public const int CanCode = 272; // Core address of line cancel code
        public const int ILfront = 286; // Core address of IL code address
        public const int BadOp = 15; // illegal op, default IL code
        public const int DEBUGON = 1; // 1 enables \t Debugging toggle, 0 disables
        public const int LOGSIZE = 4096; // how much to log

        // Interpreter states
        public const int INTERP_DONE = 0;
        public const int INTERP_NEEDLINE = 1;
        public const int INTERP_NEEDCHAR = 2;
        public const int INTERP_TIMESLICE_FINISHED = 3;

        public const int TIMESLICE_INSTRUCTION_COUNT = 10000;

        public TinyBasic(IConsoleIO consoleIO)
        {
            this.consoleIO = consoleIO;
        }

        private bool Broken()
        {
            bool result = broken;
            broken = false;
            return result;
        }

        public void setBroken(bool broken)
        {
            this.broken = broken;
        }

        private void IoFileClose(FileType fi)
        {
            fi.close();
        }

        private char InFileChar(FileType fi)
        {
            return fi.inChar();
        }

        private void OutFileChar(FileType fi, char ch)
        {
            fi.outChar(ch);
        }

        private void ScreenChar(char ch)
        {
            consoleIO.screenChar(ch);
        }

        private char KeyInChar()
        {
            return consoleIO.read();
        }

        private bool StopIt()
        {
            return Broken();
        } // ~StopIt

        private void OutStr(string theMsg)
        { // output a string to the console
            consoleIO.print(theMsg);
        } // ~OutStr

        public const bool NeedsEcho = false;

        private char CfileRead(FileType fi)
        { // C file reader, returns '\0' on eof
            return fi.read();
        }

        // External interface
        private IConsoleIO consoleIO;
        private bool broken;

        // Constants:

        // debugging stuff...
        public static int Debugging = 0; // >0 enables debug code
        private int[] DebugLog = new int[LOGSIZE]; // quietly logs recent activity
        private int LogHere = 0; // current index in DebugLog
        private int Watcher = 0, Watchee; // memory watchpoint

        // Static/global data:
        private int[] Core = new int[CoreTop]; // everything goes in here
        private int[] DeCaps = new int[128]; // capitalization table
        private int Lino, ILPC; // current line #, IL program counter
        private int BP, SvPt; // current, saved TB parse pointer
        private int SubStk, ExpnTop; // stack pointers
        private int InLend, SrcEnd; // current input line & TB source end
        private int UserEnd;
        private int ILend, XQhere; // end of IL code, start of execute loop
        private FileType inFile = null; // from option '-i' or user menu/button
        private FileType oFile = null; // from option '-o' or user menu/button

        //************************ Memory Utilities.. *************************/

        private void Poke2(int loc, int valu)
        { // store integer as two bytes
            Core[loc] = (int)((valu >> 8) & 255); // nominally Big-Endian
            Core[loc + 1] = (int)(valu & 255);
        } // ~Poke2

        private int Peek2(int loc)
        { // fetch integer from two bytes
            return ((int)Core[loc]) * 256 + ((int)Core[loc + 1]);
        } // ~Peek2

        //************************* I/O Utilities... **************************/

        private void Ouch(char ch)
        { // output char to stdout
            if (oFile != null)
            { // there is an output file..
                if (ch >= ' ')
                    OutFileChar(oFile, ch);
                else if (ch == '\r')
                    OutFileChar(oFile, '\n');
            }
            if (ch == '\r')
            {
                Core[TabHere] = 0; // keep count of how long this line is
                ScreenChar('\n');
            }
            else if (ch >= ' ')
                if (ch <= '~')
                { // ignore non-print control chars
                    Core[TabHere]++;
                    ScreenChar(ch);
                }
        } // ~Ouch

        private char Inch()
        { // read input character from stdin or file
            char ch;
            if (inFile != null)
            { // there is a file to get input from..
                ch = InFileChar(inFile);
                if (ch == '\n')
                    ch = '\r';
                if (ch == '\0')
                { // switch over to console input at eof
                    IoFileClose(inFile);
                    inFile = null;
                }
                else
                {
                    Ouch(ch); // echo input to screen (but not output file)
                    return ch;
                }
            }
            ch = KeyInChar(); // get input from stdin
            if (NeedsEcho)
                ScreenChar(ch); // alternative input may need this
            if (oFile != null)
                OutFileChar(oFile, ch); // echo it to output file
            if (ch == '\n')
            {
                ch = '\r'; // convert line end to TB standard
                Core[TabHere] = 0;
            } // reset tab counter
            return ch;
        } // ~Inch

        private void OutLn()
        { // terminate output line to the console
            OutStr("\r");
        } // ~OutLn

        private void OutInt(int theNum)
        { // output a number to the console
            if (theNum < 0)
            {
                Ouch('-');
                theNum = -theNum;
            }
            if (theNum > 9)
                OutInt(theNum / 10);
            Ouch((char)(theNum % 10 + 48));
        } // ~OutInt

        //********************** Debugging Utilities... ***********************/

        private void OutHex(int num, int nd)
        { // output a hex number to the console
            if (nd > 1)
                OutHex(num >> 4, nd - 1);
            num = num & 15;
            if (num > 9)
                Ouch((char)(num + 55));
            else
                Ouch((char)(num + 48));
        } // ~OutHex

        private void ShowSubs()
        { // display subroutine stack for debugging
            int ix;
            OutLn();
            OutStr(" [Stk ");
            OutHex(SubStk, 5);
            for (ix = SubStk; ix < UserEnd; ix++)
            {
                OutStr(" ");
                OutInt(Peek2(ix++));
            }
            OutStr("]");
        } // ~ShowSubs

        private void ShowExSt()
        { // display expression stack for debugging
            int ix;
            OutLn();
            OutStr(" [Exp ");
            OutHex(ExpnTop, 3);
            if ((ExpnTop & 1) == 0)
                for (ix = ExpnTop; ix < ExpnStk; ix++)
                {
                    OutStr(" ");
                    OutInt((int)((short)Peek2(ix++)));
                }
            else
                for (ix = ExpnTop; ix < ExpnStk; ix++)
                {
                    OutStr(".");
                    OutInt((int)Core[ix]);
                }
            OutStr("]");
        } // ~ShowExSt

        private void ShowVars(int whom)
        { // display vars for debugging
            int ix, valu = 1, prior = 1;
            if (whom == 0)
                whom = 26;
            else
            {
                whom = (whom >> 1) & 31; // whom is a specified var, or 0
                valu = whom;
            }
            OutLn();
            OutStr("  [Vars");
            for (ix = valu; ix <= whom; ix++)
            { // all non-zero vars, or else whom
                valu = (int)((short)Peek2(ix * 2 + ExpnStk));
                if (valu == 0)
                    if (prior == 0)
                        continue; // omit multiple 0s
                prior = valu;
                OutStr(" ");
                Ouch((char)(ix + 64)); // show var name
                OutStr("=");
                OutInt(valu);
            }
            OutStr("]");
        } // ~ShowVars

        private void ShoMemDump(int here, int nlocs)
        { // display hex memory dump
            int temp, thar = here & -16;
            while (nlocs > 0)
            {
                temp = thar;
                OutLn();
                OutHex(here, 4);
                OutStr(": ");
                while (thar < here)
                {
                    OutStr("   ");
                    thar++;
                }
                do
                {
                    OutStr(" ");
                    if (nlocs-- > 0)
                        OutHex(Core[here], 2);
                    else
                        OutStr("  ");
                }
                while (++here % 16 != 0);
                OutStr("  ");
                while (temp < thar)
                {
                    OutStr(" ");
                    temp++;
                }
                while (thar < here)
                {
                    if (nlocs < 0)
                        if ((thar & 15) >= nlocs + 16)
                            break;
                    temp = Core[thar++];
                    if (temp == (int)'\r')
                        Ouch('\\');
                    else if (temp < 32)
                        Ouch('`');
                    else if (temp > 126)
                        Ouch('~');
                    else
                        Ouch((char)temp);
                }
            }
            OutLn();
        } // ~ShoMemDump

        private void ShoLogVal(int item)
        { // format & output one activity log item
            int valu = DebugLog[item];
            OutLn();
            if (valu < -65536)
            { // store to a variable
                Ouch((char)(((valu >> 17) & 31) + 64));
                OutStr("=");
                OutInt((valu & 0x7FFF) - (valu & 0x8000));
            }
            else if (valu < -32768)
            { // error #
                OutStr("Err ");
                OutInt(-valu - 32768);
            }
            else if (valu < 0)
            { // only logs IL sequence changes
                OutStr("  IL+");
                OutHex(-Peek2(ILfront) - valu, 3);
            }
            else if (valu < 65536)
            { // TinyBasic line #
                OutStr("#");
                OutInt(valu);
            }
            else
            { // poke memory byte
                OutStr("!");
                OutHex(valu, 4);
                OutStr("=");
                OutInt(valu >> 16);
            }
        } // ~ShoLogVal

        private void ShowLog()
        { // display activity log for debugging
            int ix;
            OutLn();
            OutStr("*** Activity Log @ ");
            OutInt(LogHere);
            OutStr(" ***");
            if (LogHere >= LOGSIZE) // circular, show only last 4K activities
                for (ix = (LogHere & (LOGSIZE - 1)); ix < LOGSIZE; ix++)
                    ShoLogVal(ix);
            for (ix = 0; ix < (LogHere & (LOGSIZE - 1)); ix++)
                ShoLogVal(ix);
            OutLn();
            OutStr("*****");
            OutLn();
        } // ~ShowLog

        private void LogIt(int valu)
        { // insert this valu into activity log
            DebugLog[(LogHere++) & (LOGSIZE - 1)] = valu;
        }

        //*********************** Utility functions... ************************/

        private void WarmStart()
        { // initialize existing program
            UserEnd = Peek2(EndUser);
            SubStk = UserEnd; // empty subroutine, expression stacks
            Poke2(GoStkTop, SubStk);
            ExpnTop = ExpnStk;
            Lino = 0; // not in any line
            ILPC = 0; // start IL at front
            SvPt = InLine;
            BP = InLine;
            Core[BP] = 0;
            Core[TabHere] = 0;
            InLend = InLine;
        } // ~WarmStart

        private void ColdStart()
        { // initialize program to empty
            if (Peek2(ILfront) != ILfront + 2)
                ILend = Peek2(ILfront) + 0x800;
            Poke2(UserProg, (ILend + 255) & -256); // start Basic shortly after IL
            if (CoreTop > 65535)
            {
                Poke2(EndUser, 65534);
                Poke2(65534, 0xDEAD);
            }
            else
                Poke2(EndUser, CoreTop);
            WarmStart();
            SrcEnd = Peek2(UserProg);
            Poke2(SrcEnd++, 0);
            Poke2(EndProg, ++SrcEnd);
        } // ~ColdStart

        private void TBerror()
        { // report interpreter error
            if (ILPC == 0)
                return; // already reported it
            OutLn();
            LogIt(-ILPC - 32768);
            OutStr("Tiny Basic error #"); // IL address is the error #
            OutInt(ILPC - Peek2(ILfront));
            if (Lino > 0)
            { // Lino=0 if in command line
                OutStr(" at line ");
                OutInt(Lino);
            }
            OutLn();
            if (Debugging > 0)
            { // some extra info if debugging..
                ShowSubs();
                ShowExSt();
                ShowVars(0);
                OutStr(" [BP=");
                OutHex(BP, 4);
                OutStr(", TB@");
                OutHex(Peek2(UserProg), 4);
                OutStr(", IL@");
                OutHex(Peek2(ILfront), 4);
                OutStr("]");
                ShoMemDump((BP - 30) & -16, 64);
            }
            Lino = 0; // restart interpreter at front
            ExpnTop = ExpnStk; // with empty expression stack
            ILPC = 0; // cheap error test; interp reloads it from ILfront
            BP = InLine;
        } // ~TBerror

        private void PushSub(int valu)
        { // push value onto Gosub stack
            if (SubStk <= SrcEnd)
                TBerror(); // overflow: bumped into program end
            else
            {
                SubStk = SubStk - 2;
                Poke2(GoStkTop, SubStk);
                Poke2(SubStk, valu);
            }
            if (Debugging > 0)
                ShowSubs();
        } // ~PushSub

        private int PopSub()
        { // pop value off Gosub stack
            if (SubStk >= Peek2(EndUser) - 1)
            { // underflow (nothing in stack)..
                TBerror();
                return -1;
            }
            else
            {
                if (Debugging > 1)
                    ShowSubs();
                SubStk = SubStk + 2;
                Poke2(GoStkTop, SubStk);
                return Peek2(SubStk - 2);
            }
        } // ~PopSub

        private void PushExBy(int valu)
        { // push byte onto expression stack
            if (ExpnTop <= InLend)
                TBerror(); // overflow: bumped into input line
            else
                Core[--ExpnTop] = (int)(valu & 255);
            if (Debugging > 0)
                ShowExSt();
        } // ~PushExBy

        private int PopExBy()
        { // pop byte off expression stack
            if (ExpnTop < ExpnStk)
                return (int)Core[ExpnTop++];
            TBerror(); // underflow (nothing in stack)
            return -1;
        } // ~PopExBy

        private void PushExInt(int valu)
        { // push integer onto expression stack
            ExpnTop = ExpnTop - 2;
            if (ExpnTop < InLend)
                TBerror(); // overflow: bumped into input line
            else
                Poke2(ExpnTop, valu);
            if (Debugging > 0)
                ShowExSt();
        } // ~PushExInt

        private int PopExInt()
        { // pop integer off expression stack
            if (++ExpnTop < ExpnStk)
                return (int)((short)Peek2((ExpnTop++) - 1));
            TBerror(); // underflow (nothing in stack)
            return -1;
        } // ~PopExInt

        private int DeHex(string txt, int start, int ndigs)
        { // decode hex -> int
            int num = 0;
            char ch = ' ';
            int pos = start;
            while (ch < '0')
                // first skip to num...
                if (ch == '\0')
                    return -1;
                else
                    ch = (char)DeCaps[txt[pos++] & 127];
            if (ch > 'F' || ch > '9' && ch < 'A')
                return -1; // not hex
            while ((ndigs--) > 0)
            { // only get requested digits
                if (ch < '0' || ch > 'F')
                    return num; // not a hex digit
                if (ch >= 'A')
                    num = num * 16 - 55 + ((int)ch); // A-F
                else if (ch <= '9')
                    num = num * 16 - 48 + ((int)ch); // 0-9
                else
                    return num; // something in between, i.e. not hex
                ch = (char)DeCaps[txt[pos++] & 127];
            }
            return num;
        } // ~DeHex

        private int SkipTo(int here, char fch)
        { // search for'd past next marker
            while (true)
            {
                char ch = (char)Core[here++]; // look at next char
                if (ch == fch)
                    return here; // got it
                if (ch == '\0')
                    return --here;
            }
        } // ~SkipTo

        private int FindLine(int theLine)
        { // find theLine in TB source code
            int ix;
            int here = Peek2(UserProg); // start at front
            while (true)
            {
                ix = Peek2(here++);
                if (theLine <= ix || ix == 0)
                    return --here; // found it or overshot
                here = SkipTo(++here, '\r');
            } // skip to end of this line
        } // ~FindLine

        private void GoToLino()
        { // find line # Lino and set BP to its front
            int here;
            if (Lino <= 0)
            { // Lino=0 is just command line (OK)..
                BP = InLine;
                if (DEBUGON > 0)
                    LogIt(0);
                return;
            }
            if (DEBUGON > 0)
                LogIt(Lino);
            if (Debugging > 0)
            {
                OutStr(" [#");
                OutInt(Lino);
                OutStr("]");
            }
            BP = FindLine(Lino); // otherwise try to find it..
            here = Peek2(BP++);
            if (here == 0)
                TBerror(); // ran off the end, error off
            else if (Lino != here)
                TBerror(); // not there
            else
                BP++;
        } // ~GoToLino                             // got it

        private void ListIt(int frm, int too)
        { // list the stored program
            char ch;
            int here;
            if (frm == 0)
            { // 0,0 defaults to all; n,0 defaults to n,n
                too = 65535;
                frm = 1;
            }
            else if (too == 0)
                too = frm;
            here = FindLine(frm); // try to find first line..
            while (!StopIt())
            {
                frm = Peek2(here++); // get this line's # to print it
                if (frm > too || frm == 0)
                    break;
                here++;
                OutInt(frm);
                Ouch(' ');
                do
                { // print the text
                    ch = (char)Core[here++];
                    Ouch(ch);
                }
                while (ch > '\r');
            }
        } // ~ListIt

        private void ConvtIL(string txt)
        { // convert & load TBIL code
            int valu;
            ILend = ILfront + 2;
            Poke2(ILfront, ILend); // initialize pointers as promised in TBEK
            Poke2(ColdGo + 1, ILend);
            Core[ILend] = (int)BadOp; // illegal op, in case nothing loaded
            if (txt == null)
                return;
            int pos = 0;
            while (pos < txt.Length)
            { // get the data..
                while (pos < txt.Length && txt[pos] > '\r')
                    pos++; // (no code on 1st line)
                if (pos++ >= txt.Length)
                    break; // no code at all
                while (pos < txt.Length && txt[pos] > ' ')
                    pos++; // skip over address
                if (pos++ >= txt.Length)
                    break;
                while (true)
                {
                    valu = DeHex(txt, pos++, 2); // get a byte
                    if (valu < 0)
                        break; // no more on this line
                    Core[ILend++] = (int)valu; // insert this byte into code
                    pos++;
                }
            }
            XQhere = 0; // requires new XQ to initialize
            Core[ILend] = 0;
        } // ~ConvtIL

        private bool ReadLine()
        {
            while (true)
            { // read input line characters...
                char ch = Inch();
                if (ch == '\0')
                    return false;

                if (ch == '\r')
                    break; // end of the line
                else if (ch == '\t')
                {
                    Debugging = (Debugging + DEBUGON) & 1; // maybe toggle debug
                    ch = ' ';
                } // convert tabs to space
                else if (ch == (char)Core[BScode])
                { // backspace code
                    if (InLend > InLine)
                        InLend--; // assume console already
                    else
                    { // backing up over front of line: just kill it..
                        Ouch('\r');
                        break;
                    }
                }
                else if (ch == (char)Core[CanCode])
                { // cancel this line
                    InLend = InLine;
                    Ouch('\r'); // also start a new input line
                    break;
                }
                else if (ch < ' ')
                    continue; // ignore non-ASCII & controls
                else if (ch > '~')
                    continue;
                if (InLend > ExpnTop - 2)
                    continue; // discard overrun chars
                Core[InLend++] = (int)ch;
            } // insert this char in buffer
            while (InLend > InLine && Core[InLend - 1] == ' ')
                InLend--; // delete excess trailing spaces
            Core[InLend++] = (int)'\r'; // insert final return & null
            Core[InLend] = 0;
            BP = InLine;
            return true;
        }

        private bool ReadChar()
        {
            char ch = Inch();
            if (ch == '\0')
                return false;
            PushExInt((int)Inch());
            return true;
        }

        private void LineSwap(int here)
        { // swap SvPt/BP if here is not in InLine
            if (here < InLine || here >= InLend)
            {
                here = SvPt;
                SvPt = BP;
                BP = here;
            }
            else
                SvPt = BP;
        } // ~LineSwap

        //************************* Main Interpreter **************************/

        private int Interp()
        {
            char ch; // comments from TinyBasic Experimenter's Kit, pp.15-21
            int op, ix, here, chpt; // temps

            int inscount = 0;

            while (true)
            {
                if (inscount++ == TIMESLICE_INSTRUCTION_COUNT)
                    return INTERP_TIMESLICE_FINISHED;

                if (StopIt())
                {
                    OutLn();
                    OutStr("*** User Break ***");
                    TBerror();
                }
                if (ILPC == 0)
                {
                    ILPC = Peek2(ILfront);
                    if (DEBUGON > 0)
                        LogIt(-ILPC);
                    if (Debugging > 0)
                    {
                        OutLn();
                        OutStr("[IL=");
                        OutHex(ILPC, 4);
                        OutStr("]");
                    }
                }
                if (DEBUGON > 0)
                    if (Watcher > 0)
                    { // check watchpoint..
                        if (((Watchee < 0) && (Watchee + 256 + (int)Core[Watcher]) != 0)
                            || ((Watchee >= 0) && (Watchee == (int)Core[Watcher])))
                        {
                            OutLn();
                            OutStr("*** Watched ");
                            OutHex(Watcher, 4);
                            OutStr(" = ");
                            OutInt((int)Core[Watcher]);
                            OutStr(" *** ");
                            Watcher = 0;
                            TBerror();
                            continue;
                        }
                    }
                op = (int)Core[ILPC++];
                if (Debugging > 0)
                {
                    OutLn();
                    OutStr("[IL+");
                    OutHex(ILPC - Peek2(ILfront) - 1, 3);
                    OutStr("=");
                    OutHex(op, 2);
                    OutStr("]");
                }
                switch (op >> 5)
                {
                    default:
                        switch (op)
                        {
                            case 15:
                                TBerror();
                                return INTERP_DONE;

                            // SX n    00-07   Stack Exchange.
                            //                 Exchange the top byte of computational stack with
                            // that "n" bytes into the stack. The top/left byte of the stack is 
                            // considered to be byte 0, so SX 0 does nothing.                   
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                            case 5:
                            case 6:
                            case 7:
                                if (ExpnTop + op >= ExpnStk)
                                { // swap is below stack depth
                                    TBerror();
                                    return INTERP_DONE;
                                }
                                ix = (int)Core[ExpnTop];
                                Core[ExpnTop] = Core[ExpnTop + op];
                                Core[ExpnTop + op] = (int)ix;
                                if (Debugging > 0)
                                    ShowExSt();
                                break;

                            // LB n    09nn    Push Literal Byte onto Stack.                    
                            //                 This adds one byte to the expression stack, which
                            // is the second byte of the instruction. An error stop will occur if
                            // the stack overflows.
                            case 9:
                                PushExBy((int)Core[ILPC++]); // push IL byte
                                break;

                            // LN n    0Annnn  Push Literal Number.                             
                            //                 This adds the following two bytes to the         
                            // computational stack, as a 16-bit number. Stack overflow results in
                            // an error stop. Numbers are assumed to be Big-Endian.             
                            case 10:
                                PushExInt(Peek2(ILPC++)); // get next 2 IL bytes
                                ILPC++;
                                break;

                            // DS      0B      Duplicate Top Number (two bytes) on Stack.       
                            //                 An error stop will occur if there are less than 2
                            // bytes (1 int) on the expression stack or if the stack overflows. 
                            case 11:
                                op = ExpnTop;
                                ix = PopExInt();
                                if (ILPC == 0)
                                    break; // underflow
                                ExpnTop = op;
                                PushExInt(ix);
                                break;

                            // SP      0C      Stack Pop.                                       
                            //                 The top two bytes are removed from the expression
                            // stack and discarded. Underflow results in an error stop.         
                            case 12:
                                ix = PopExInt();
                                if (Debugging > 0)
                                    ShowExSt();
                                break;

                            // SB      10      Save BASIC Pointer.                              
                            //                 If BASIC pointer is pointing into the input line 
                            // buffer, it is copied to the Saved Pointer; otherwise the two     
                            // pointers are exchanged.                                          
                            case 16:
                                LineSwap(BP);
                                break;

                            // RB      11      Restore BASIC Pointer.                           
                            //                 If the Saved Pointer points into the input line  
                            // buffer, it is replaced by the value in the BASIC pointer;        
                            // otherwise the two pointers are exchanged.                        
                            case 17:
                                LineSwap(SvPt);
                                break;

                            // FV      12      Fetch Variable.                                  
                            //                 The top byte of the computational stack is used to
                            // index into Page 00. It is replaced by the two bytes fetched. Error
                            // stops occur with stack overflow or underflow.                    
                            case 18:
                                op = PopExBy();
                                if (ILPC != 0)
                                    PushExInt(Peek2(op));
                                if (Debugging > 1)
                                    ShowVars(op);
                                break;

                            // SV      13      Store Variable.                                  
                            //                 The top two bytes of the computational stack are 
                            // stored into memory at the Page 00 address specified by the third 
                            // byte on the stack. All three bytes are deleted from the stack.   
                            // Underflow results in an error stop.                              
                            case 19:
                                ix = PopExInt();
                                op = PopExBy();
                                if (ILPC == 0)
                                    break;
                                Poke2(op, ix);
                                if (DEBUGON > 0)
                                    LogIt((ix & 0xFFFF) + ((op - 256) << 16));
                                if (Debugging > 0)
                                {
                                    ShowVars(op);
                                    if (Debugging > 1)
                                        ShowExSt();
                                }
                                break;

                            // GS      14      GOSUB Save.                                      
                            //                 The current BASIC line number is pushed          
                            // onto the BASIC region of the control stack. It is essential that 
                            // the IL stack be empty for this to work properly but no check is  
                            // made for that condition. An error stop occurs on stack overflow. 
                            case 20:
                                PushSub(Lino); // push line # (possibly =0)
                                break;

                            // RS      15      Restore Saved Line.                              
                            //                 Pop the top two bytes off the BASIC region of the
                            // control stack, making them the current line number. Set the BASIC
                            // pointer at the beginning of that line. Note that this is the line
                            // containing the GOSUB which caused the line number to be saved. As
                            // with the GS opcode, it is essential that the IL region of the    
                            // control stack be empty. If the line number popped off the stack  
                            // does not correspond to a line in the BASIC program an error stop 
                            // occurs. An error stop also results from stack underflow.         
                            case 21:
                                Lino = PopSub(); // get line # (possibly =0) from pop
                                if (ILPC != 0)
                                    GoToLino(); // stops run if error
                                break;

                            // GO      16      GOTO.                                            
                            //                 Make current the BASIC line whose line number is 
                            // equal to the value of the top two bytes in the expression stack. 
                            // That is, the top two bytes are popped off the computational stack,
                            // and the BASIC program is searched until a matching line number is
                            // found. The BASIC pointer is then positioned at the beginning of  
                            // that line and the RUN mode flag is turned on. Stack underflow and
                            // non-existent BASIC line result in error stops.                   
                            case 22:
                                ILPC = XQhere; // the IL assumes an implied NX
                                if (DEBUGON > 0)
                                    LogIt(-ILPC);
                                Lino = PopExInt();
                                if (ILPC != 0)
                                    GoToLino(); // stops run if error
                                break;

                            // NE      17      Negate (two's complement).                       
                            //                 The number in the top two bytes of the expression
                            // stack is replaced with its negative.                             
                            case 23:
                                ix = PopExInt();
                                if (ILPC != 0)
                                    PushExInt(-ix);
                                break;

                            // AD      18      Add.                                             
                            //                 Add the two numbers represented by the top four  
                            // bytes of the expression stack, and replace them with the two-byte
                            // sum. Stack underflow results in an error stop.                   
                            case 24:
                                ix = PopExInt();
                                op = PopExInt();
                                if (ILPC != 0)
                                    PushExInt(op + ix);
                                break;

                            // SU      19      Subtract.                                        
                            //                 Subtract the two-byte number on the top of the   
                            // expression stack from the next two bytes and replace the 4 bytes 
                            // with the two-byte difference.                                    
                            case 25:
                                ix = PopExInt();
                                op = PopExInt();
                                if (ILPC != 0)
                                    PushExInt(op - ix);
                                break;

                            // MP      1A      Multiply.                                        
                            //                 Multiply the two numbers represented by the top 4
                            // bytes of the computational stack, and replace them with the least
                            // significant 16 bits of the product. Stack underflow is possible. 
                            case 26:
                                ix = PopExInt();
                                op = PopExInt();
                                if (ILPC != 0)
                                    PushExInt(op * ix);
                                break;

                            // DV      1B      Divide.                                          
                            //                 Divide the number represented by the top two bytes
                            // of the computational stack into that represented by the next two.
                            // Replace the 4 bytes with the quotient and discard the remainder. 
                            // This is a signed (two's complement) integer divide, resulting in a
                            // signed integer quotient. Stack underflow or attempted division by
                            // zero result in an error stop.
                            case 27:
                                ix = PopExInt();
                                op = PopExInt();
                                if (ix == 0)
                                    TBerror(); // divide by 0..
                                else if (ILPC != 0)
                                    PushExInt(op / ix);
                                break;

                            // CP      1C      Compare.                                         
                            //                 The number in the top two bytes of the expression
                            // stack is compared to (subtracted from) the number in the 4th and 
                            // fifth bytes of the stack, and the result is determined to be     
                            // Greater, Equal, or Less. The low three bits of the third byte mask
                            // a conditional skip in the IL program to test these conditions; if
                            // the result corresponds to a one bit, the next byte of the IL code
                            // is skipped and not executed. The three bits correspond to the    
                            // conditions as follows:                                           
                            //         bit 0   Result is Less                                   
                            //         bit 1   Result is Equal                                  
                            //         bit 2   Result is Greater                                
                            // Whether the skip is taken or not, all five bytes are deleted from
                            // the stack. This is a signed (two's complement) comparison so that
                            // any positive number is greater than any negative number. Multiple
                            // conditions, such as greater-than-or-equal or unequal (i.e.greater-
                            // than-or-less-than), may be tested by forming the condition mask  
                            // byte of the sum of the respective bits. In particular, a mask byte
                            // of 7 will force an unconditional skip and a mask byte of 0 will  
                            // force no skip. The other 5 bits of the control byte are ignored. 
                            // Stack underflow results in an error stop.                        
                            case 28:
                                ix = PopExInt();
                                op = PopExBy();
                                ix = PopExInt() - ix; // <0 or =0 or >0
                                if (ILPC == 0)
                                    return INTERP_DONE; // underflow..
                                if (ix < 0)
                                    ix = 1;
                                else if (ix > 0)
                                    ix = 4; // choose the bit to test
                                else
                                    ix = 2;
                                if ((ix & op) > 0)
                                    ILPC++; // skip next IL op if bit =1
                                if (Debugging > 0)
                                    ShowExSt();
                                break;

                            // NX      1D      Next BASIC Statement.                            
                            //                 Advance to next line in the BASIC program, if in 
                            // RUN mode, or restart the IL program if in the command mode. The  
                            // remainder of the current line is ignored. In the Run mode if there
                            // is another line it becomes current with the pointer positioned at
                            // its beginning. At this time, if the Break condition returns true,
                            // execution is aborted and the IL program is restarted after       
                            // printing an error message. Otherwise IL execution proceeds from  
                            // the saved IL address (see the XQ instruction). If there are no   
                            // more BASIC statements in the program an error stop occurs.       
                            case 29:
                                if (Lino == 0)
                                    ILPC = 0;
                                else
                                {
                                    BP = SkipTo(BP, '\r'); // skip to end of this line
                                    Lino = Peek2(BP++); // get line #
                                    if (Lino == 0)
                                    { // ran off the end
                                        TBerror();
                                        break;
                                    }
                                    else
                                        BP++;
                                    ILPC = XQhere; // restart at saved IL address (XQ)
                                    if (DEBUGON > 0)
                                        LogIt(-ILPC);
                                }
                                if (DEBUGON > 0)
                                    LogIt(Lino);
                                if (Debugging > 0)
                                {
                                    OutStr(" [#");
                                    OutInt(Lino);
                                    OutStr("]");
                                }
                                break;

                            // LS      1F      List The Program.                                
                            //                 The expression stack is assumed to have two 2-byte
                            // numbers. The top number is the line number of the last line to be
                            // listed, and the next is the line number of the first line to be  
                            // listed. If the specified line numbers do not exist in the program,
                            // the next available line (i.e. with the next higher line number) is
                            // assumed instead in each case. If the last line to be listed comes
                            // before the first, no lines are listed. If Break condition comes  
                            // true during a List operation, the remainder of the listing is    
                            // aborted. Zero is not a valid line number, and an error stop occurs
                            // if either line number specification is zero. The line number     
                            // specifications are deleted from the stack.                       
                            case 31:
                                op = 0;
                                ix = 0; // The IL seems to assume we can handle zero
                                while (ExpnTop < ExpnStk)
                                { // or more numbers, so get them..
                                    op = ix;
                                    ix = PopExInt();
                                } // get final line #, then initial..
                                if (op < 0 || ix < 0)
                                    TBerror();
                                else
                                    ListIt(ix, op);
                                break;

                            // PN      20      Print Number.                                    
                            //                 The number represented by the top two bytes of the
                            // expression stack is printed in decimal with leading zero         
                            // suppression. If it is negative, it is preceded by a minus sign   
                            // and the magnitude is printed. Stack underflow is possible.       
                            case 32:
                                ix = PopExInt();
                                if (ILPC != 0)
                                    OutInt(ix);
                                break;

                            // PQ      21      Print BASIC string.                              
                            //                 The ASCII characters beginning with the current  
                            // position of BASIC pointer are printed on the console. The string 
                            // to be printed is terminated by quotation mark ("), and the BASIC 
                            // pointer is left at the character following the terminal quote. An
                            // error stop occurs if a carriage return is imbedded in the string.
                            case 33:
                                while (true)
                                {
                                    ch = (char)Core[BP++];
                                    if (ch == '\"')
                                        break; // done on final quote
                                    if (ch < ' ')
                                    { // error if return or other control char
                                        TBerror();
                                        break;
                                    }
                                    Ouch(ch);
                                } // print it
                                break;

                            // PT      22      Print Tab.                                       
                            //                 Print one or more spaces on the console, ending at
                            // the next multiple of eight character positions (from the left    
                            // margin).                                                         
                            case 34:
                                do
                                {
                                    Ouch(' ');
                                }
                                while (Core[TabHere] % 8 > 0);
                                break;

                            // NL      23      New Line.                                        
                            //                 Output a carriage-return-linefeed sequence to the
                            // console.                                                         
                            case 35:
                                Ouch('\r');
                                break;

                            // PC "xxxx"  24xxxxxxXx   Print Literal string.                    
                            //                         The ASCII string follows opcode and its  
                            // last byte has the most significant bit set to one.               
                            case 36:
                                do
                                {
                                    ix = (int)Core[ILPC++];
                                    Ouch((char)(ix & 127)); // strip high bit for output
                                }
                                while ((ix & 128) == 0);
                                break;

                            // GL      27      Get Input Line.                                  
                            //                 ASCII characters are accepted from console input 
                            // to fill the line buffer. If the line length exceeds the available
                            // space, the excess characters are ignored and bell characters are 
                            // output. The line is terminated by a carriage return. On completing
                            // one line of input, the BASIC pointer is set to point to the first
                            // character in the input line buffer, and a carriage-return-linefeed
                            // sequence is [not] output.                                        
                            case 39:
                                InLend = InLine;
                                bool result = ReadLine();
                                if (!result)
                                    return INTERP_NEEDLINE;
                                break;

                            // IL      2A      Insert BASIC Line.                               
                            //                 Beginning with the current position of the BASIC 
                            // pointer and continuing to the [end of it], the line is inserted  
                            // into the BASIC program space; for a line number, the top two bytes
                            // of the expression stack are used. If this number matches a line  
                            // already in the program it is deleted and the new one replaces it.
                            // If the new line consists of only a carriage return, it is not    
                            // inserted, though any previous line with the same number will have
                            // been deleted. The lines are maintained in the program space sorted
                            // by line number. If the new line to be inserted is a different size
                            // than the old line being replaced, the remainder of the program is
                            // shifted over to make room or to close up the gap as necessary. If
                            // there is insufficient memory to fit in the new line, the program 
                            // space is unchanged and an error stop occurs (with the IL address 
                            // decremented). A normal error stop occurs on expression stack     
                            // underflow or if the number is zero, which is not a valid line    
                            // number. After completing the insertion, the IL program is        
                            // restarted in the command mode.                                   
                            case 42:
                                Lino = PopExInt(); // get line #
                                if (Lino <= 0)
                                { // don't insert line #0 or negative
                                    if (ILPC != 0)
                                        TBerror();
                                    else
                                        return INTERP_DONE;
                                    break;
                                }
                                while (((char)Core[BP]) == ' ')
                                    BP++; // skip leading spaces
                                if (((char)Core[BP]) == '\r')
                                    ix = 0; // nothing to add
                                else
                                    ix = InLend - BP + 2; // the size of the insertion
                                op = 0; // this will be the number of bytes to delete
                                chpt = FindLine(Lino); // try to find this line..
                                if (Peek2(chpt) == Lino) // there is a line to delete..
                                    op = (SkipTo(chpt + 2, '\r') - chpt);
                                if (ix == 0)
                                    if (op == 0)
                                    { // nothing to add nor delete; done
                                        Lino = 0;
                                        break;
                                    }
                                op = ix - op; // = how many more bytes to add or (-)delete
                                if (SrcEnd + op >= SubStk)
                                { // too big..
                                    TBerror();
                                    break;
                                }
                                SrcEnd = SrcEnd + op; // new size
                                if (op > 0)
                                    for (here = SrcEnd; (here--) > chpt + ix;)
                                        Core[here] = Core[here - op]; // shift backend over to right
                                else if (op < 0)
                                    for (here = chpt + ix; here < SrcEnd; here++)
                                        Core[here] = Core[here - op]; // shift it left to close gap
                                if (ix > 0)
                                    Poke2(chpt++, Lino); // insert the new line #
                                while (ix > 2)
                                { // insert the new line..
                                    Core[++chpt] = Core[BP++];
                                    ix--;
                                }
                                Poke2(EndProg, SrcEnd);
                                ILPC = 0;
                                Lino = 0;
                                if (Debugging > 0)
                                    ListIt(0, 0);
                                break;

                            // MT      2B      Mark the BASIC program space Empty.              
                            //                 Also clears the BASIC region of the control stack
                            // and restart the IL program in the command mode. The memory bounds
                            // and stack pointers are reset by this instruction to signify empty
                            // program space, and the line number of the first line is set to 0,
                            // which is the indication of the end of the program.               
                            case 43:
                                ColdStart();
                                if (Debugging > 0)
                                {
                                    ShowSubs();
                                    ShowExSt();
                                    ShowVars(0);
                                }
                                break;

                            // XQ      2C      Execute.                                         
                            //                 Turns on RUN mode. This instruction also saves   
                            // the current value of the IL program counter for use of the NX    
                            // instruction, and sets the BASIC pointer to the beginning of the  
                            // BASIC program space. An error stop occurs if there is no BASIC   
                            // program. This instruction must be executed at least once before  
                            // the first execution of a NX instruction.                         
                            case 44:
                                XQhere = ILPC;
                                BP = Peek2(UserProg);
                                Lino = Peek2(BP++);
                                BP++;
                                if (Lino == 0)
                                    TBerror();
                                else if (Debugging > 0)
                                {
                                    OutStr(" [#");
                                    OutInt(Lino);
                                    OutStr("]");
                                }
                                break;

                            // WS      2D      Stop.                                            
                            //                 Stop execution and restart the IL program in the 
                            // command mode. The entire control stack (including BASIC region)  
                            // is also vacated by this instruction. This instruction effectively
                            // jumps to the Warm Start entry of the ML interpreter.             
                            case 45:
                                WarmStart();
                                if (Debugging > 0)
                                    ShowSubs();
                                break;

                            // US      2E      Machine Language Subroutine Call.                
                            //                 The top six bytes of the expression stack contain
                            // 3 numbers with the following interpretations: The top number is  
                            // loaded into the A (or A and B) register; the next number is loaded
                            // into 16 bits of Index register; the third number is interpreted as
                            // the address of a machine language subroutine to be called. These 
                            // six bytes on the expression stack are replaced with the 16-bit   
                            // result returned by the subroutine. Stack underflow results in an 
                            // error stop.                                                      
                            case 46:
                                Poke2(LinoCore, Lino); // bring these memory locations up..
                                Poke2(ILPCcore, ILPC); // ..to date, in case user looks..
                                Poke2(BPcore, BP);
                                Poke2(SvPtCore, SvPt);
                                ix = PopExInt() & 0xFFFF; // datum A
                                here = PopExInt() & 0xFFFF; // datum X
                                op = PopExInt() & 0xFFFF; // nominal machine address
                                if (ILPC == 0)
                                    break;
                                if (op >= Peek2(ILfront) && op < ILend)
                                { // call IL subroutine..
                                    PushExInt(here);
                                    PushExInt(ix);
                                    PushSub(ILPC); // push return location
                                    ILPC = op;
                                    if (DEBUGON > 0)
                                        LogIt(-ILPC);
                                    break;
                                }
                                switch (op)
                                {
                                    case WachPoint: // we only do a few predefined functions..
                                        Watcher = here;
                                        if (ix > 32767)
                                            ix = -(int)Core[here] - 256;
                                        Watchee = ix;
                                        if (Debugging > 0)
                                        {
                                            OutLn();
                                            OutStr("[** Watch ");
                                            OutHex(here, 4);
                                            OutStr("]");
                                        }
                                        PushExInt((int)Core[here]);
                                        break;
                                    case ColdGo:
                                        ColdStart();
                                        break;
                                    case WarmGo:
                                        WarmStart();
                                        break;
                                    case InchSub:
                                        {
                                            if (!ReadChar())
                                                return INTERP_NEEDCHAR;
                                            break;
                                        }
                                    case OutchSub:
                                        Ouch((char)(ix & 127));
                                        PushExInt(0);
                                        break;
                                    case BreakSub:
                                        PushExInt(StopIt() ? 1 : 0);
                                        break;
                                    case PeekSub:
                                        PushExInt((int)Core[here]);
                                        break;
                                    case Peek2Sub:
                                        PushExInt(Peek2(here));
                                        break;
                                    case PokeSub:
                                        ix = ix & 0xFF;
                                        Core[here] = (int)ix;
                                        PushExInt(ix);
                                        if (DEBUGON > 0)
                                            LogIt(((ix + 256) << 16) + here);
                                        Lino = Peek2(LinoCore); // restore these pointers..
                                        ILPC = Peek2(ILPCcore); // ..in case user changed them..
                                        BP = Peek2(BPcore);
                                        SvPt = Peek2(SvPtCore);
                                        break;
                                    case DumpSub:
                                        ShoMemDump(here, ix);
                                        PushExInt(here + ix);
                                        break;
                                    case TrLogSub:
                                        ShowLog();
                                        PushExInt(LogHere);
                                        break;
                                    default:
                                        TBerror();
                                        break;
                                }
                                break;

                            // RT      2F      IL Subroutine Return.                            
                            //                 The IL control stack is popped to give the address
                            // of the next IL instruction. An error stop occurs if the entire   
                            // control stack (IL and BASIC) is empty.                           
                            case 47:
                                ix = PopSub(); // get return from pop
                                if (ix < Peek2(ILfront) || ix >= ILend)
                                    TBerror();
                                else if (ILPC != 0)
                                {
                                    ILPC = ix;
                                    if (DEBUGON > 0)
                                        LogIt(-ILPC);
                                }
                                break;

                            // JS a    3000-37FF       IL Subroutine Call.                      
                            //                         The least significant eleven bits of this
                            // 2-byte instruction are added to the base address of the IL program
                            // to become address of the next instruction. The previous contents 
                            // of the IL program counter are pushed onto the IL region of the   
                            // control stack. Stack overflow results in an error stop.          
                            case 48:
                            case 49:
                            case 50:
                            case 51:
                            case 52:
                            case 53:
                            case 54:
                            case 55:
                                PushSub(ILPC + 1); // push return location there
                                if (ILPC == 0)
                                    break;
                                ILPC = (Peek2(ILPC - 1) & 0x7FF) + Peek2(ILfront);
                                if (DEBUGON > 0)
                                    LogIt(-ILPC);
                                break;

                            // J a     3800-3FFF       Jump.                                    
                            //                         The low eleven bits of this 2-byte       
                            // instruction are added to the IL program base address to determine
                            // the address of the next IL instruction. The previous contents of 
                            // the IL program counter is lost.
                            case 56:
                            case 57:
                            case 58:
                            case 59:
                            case 60:
                            case 61:
                            case 62:
                            case 63:
                                ILPC = (Peek2(ILPC - 1) & 0x7FF) + Peek2(ILfront);
                                if (DEBUGON > 0)
                                    LogIt(-ILPC);
                                break;

                            // NO      08      No Operation.                                    
                            //                 This may be used as a space filler (such as to   
                            // ignore a skip).                                                  
                            default:
                                break;
                        } // last of inner switch cases
                        break; // end of outer switch cases 0,1

                    // BR a    40-7F   Relative Branch.                                 
                    //                 The low six bits of this instruction opcode are  
                    // added algebraically to the current value of the IL program counter
                    // to give the address of the next IL instruction. Bit 5 of opcode is
                    // the sign, with + signified by 1, - by 0. The range of this branch
                    // is +/-31 bytes from address of the byte following the opcode. An 
                    // offset of zero (i.e. opcode 60) results in an error stop. The    
                    // branch operation is unconditional.                               
                    case 2:
                    case 3:
                        ILPC = ILPC + op - 96;
                        if (DEBUGON > 0)
                            LogIt(-ILPC);
                        break;

                    // BC a "xxx"   80xxxxXx-9FxxxxXx  string Match Branch.             
                    //                                 The ASCII character string in IL 
                    // following this opcode is compared to the string beginning with the
                    // current position of the BASIC pointer, ignoring blanks in BASIC  
                    // program. The comparison continues until either a mismatch, or an 
                    // IL byte is reached with the most significant bit set to one. This
                    // is the last byte of the string in the IL, compared as a 7-bit    
                    // character; if equal, the BASIC pointer is positioned after the   
                    // last matching character in the BASIC program and the IL continues
                    // with the next instruction in sequence. Otherwise the BASIC pointer
                    // is not altered and the low five bits of the Branch opcode are    
                    // added to the IL program counter to form the address of the next  
                    // IL instruction. If the strings do not match and the branch offset
                    // is zero an error stop occurs.                                    
                    case 4:
                        if (op == 128)
                            here = 0; // to error if no match
                        else
                            here = ILPC + op - 128;
                        chpt = BP;
                        ix = 0;
                        while ((ix & 128) == 0)
                        {
                            while (((char)Core[BP]) == ' ')
                                BP++; // skip over spaces
                            ix = (int)Core[ILPC++];
                            if (((char)(ix & 127)) != DeCaps[((int)Core[BP++]) & 127])
                            {
                                BP = chpt; // back up to front of string in Basic
                                if (here == 0)
                                    TBerror();
                                else
                                    ILPC = here; // jump forward in IL
                                break;
                            }
                        }
                        if (DEBUGON > 0)
                            if (ILPC > 0)
                                LogIt(-ILPC);
                        break;

                    // BV a    A0-BF   Branch if Not Variable.                          
                    //                 If the next non-blank character pointed to by the
                    // BASIC pointer is a capital letter, its ASCII code is [doubled and]
                    // pushed onto the expression stack and the IL program advances to  
                    // next instruction in sequence, leaving the BASIC pointer positioned
                    // after the letter; if not a letter the branch is taken and BASIC  
                    // pointer is left pointing to that character. An error stop occurs 
                    // if the next character is not a letter and the offset of the branch
                    // is zero, or on stack overflow.                                   
                    case 5:
                        while (((char)Core[BP]) == ' ')
                            BP++; // skip over spaces
                        ch = (char)Core[BP];
                        if (ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z')
                            PushExBy((((int)Core[BP++]) & 0x5F) * 2);
                        else if (op == 160)
                            TBerror(); // error if not letter
                        else
                            ILPC = ILPC + op - 160;
                        if (DEBUGON > 0)
                            if (ILPC > 0)
                                LogIt(-ILPC);
                        break;

                    // BN a    C0-DF   Branch if Not a Number.                          
                    //                 If the next non-blank character pointed to by the
                    // BASIC pointer is not a decimal digit, the low five bits of the   
                    // opcode are added to the IL program counter, or if zero an error  
                    // stop occurs. If the next character is a digit, then it and all   
                    // decimal digits following it (ignoring blanks) are converted to a 
                    // 16-bit binary number which is pushed onto the expression stack. In
                    // either case the BASIC pointer is positioned at the next character
                    // which is neither blank nor digit. Stack overflow will result in an
                    // error stop.                                                      
                    case 6:
                        while (((char)Core[BP]) == ' ')
                            BP++; // skip over spaces
                        ch = (char)Core[BP];
                        if (ch >= '0' && ch <= '9')
                        {
                            op = 0;
                            while (true)
                            {
                                here = (int)Core[BP++];
                                if (here == 32)
                                    continue; // skip over spaces
                                if (here < 48 || here > 57)
                                    break; // not a decimal digit
                                op = op * 10 + here - 48;
                            } // insert into value
                            BP--; // back up over non-digit
                            PushExInt(op);
                        }
                        else if (op == 192)
                            TBerror(); // error if no digit
                        else
                            ILPC = ILPC + op - 192;
                        if (DEBUGON > 0)
                            if (ILPC > 0)
                                LogIt(-ILPC);
                        break;

                    // BE a    E0-FF   Branch if Not Endline.                           
                    //                 If the next non-blank character pointed to by the
                    // BASIC pointer is a carriage return, the IL program advances to the
                    // next instruction in sequence; otherwise the low five bits of the 
                    // opcode (if not 0) are added to the IL program counter to form the
                    // address of next IL instruction. In either case the BASIC pointer 
                    // is left pointing to the first non-blank character; this          
                    // instruction will not pass over the carriage return, which must   
                    // remain for testing by the NX instruction. As with the other      
                    // conditional branches, the branch may only advance the IL program 
                    // counter from 1 to 31 bytes; an offset of zero results in an error
                    // stop.                                                            
                    case 7:
                        while (((char)Core[BP]) == ' ')
                            BP++; // skip over spaces
                        if (((char)Core[BP]) == '\r')
                            ;
                        else if (op == 224)
                            TBerror(); // error if no offset
                        else
                            ILPC = ILPC + op - 224;
                        if (DEBUGON > 0)
                            if (ILPC > 0)
                                LogIt(-ILPC);
                        break;
                }
            }
        } // ~Interp

        //**************** Intermediate Interpreter Assembled *****************/

        private string DEFAULT_IL;

        private string DefaultIL()
        {
            if (DEFAULT_IL == null)
            {
                StringBuilder s = new StringBuilder();
                s
                    .Append("0000 ;       1 .  ORIGINAL TINY BASIC INTERMEDIATE INTERPRETER\n");
                s.Append("0000 ;       2 .\n");
                s.Append("0000 ;       3 .  EXECUTIVE INITIALIZATION\n");
                s.Append("0000 ;       4 .\n");
                s.Append("0000 ;       5 :STRT PC \":Q^\"        COLON, X-ON\n");
                s.Append("0000 243A91;\n");
                s.Append("0003 ;       6       GL\n");
                s.Append("0003 27;     7       SB\n");
                s
                    .Append("0004 10;     8       BE L0           BRANCH IF NOT EMPTY\n");
                s
                    .Append("0005 E1;     9       BR STRT         TRY AGAIN IF null LINE\n");
                s
                    .Append("0006 59;    10 :L0   BN STMT         TEST FOR LINE NUMBER\n");
                s
                    .Append("0007 C5;    11       IL              IF SO, INSERT INTO PROGRAM\n");
                s.Append("0008 2A;    12       BR STRT         GO GET NEXT\n");
                s
                    .Append("0009 56;    13 :XEC  SB              SAVE POINTERS FOR RUN WITH\n");
                s
                    .Append("000A 10;    14       RB                CONCATENATED INPUT\n");
                s.Append("000B 11;    15       XQ\n");
                s.Append("000C 2C;    16 .\n");
                s.Append("000D ;      17 .  STATEMENT EXECUTOR\n");
                s.Append("000D ;      18 .\n");
                s.Append("000D ;      19 :STMT BC GOTO \"LET\"\n");
                s.Append("000D 8B4C45D4;\n");
                s
                    .Append("0011 ;      20       BV *            MUST BE A VARIABLE NAME\n");
                s.Append("0011 A0;    21       BC * \"=\"\n");
                s
                    .Append("0012 80BD;  22 :LET  JS EXPR         GO GET EXPRESSION\n");
                s
                    .Append("0014 30BC;  23       BE *            IF STATEMENT END,\n");
                s.Append("0016 E0;    24       SV                STORE RESULT\n");
                s.Append("0017 13;    25       NX\n");
                s.Append("0018 1D;    26 .\n");
                s.Append("0019 ;      27 :GOTO BC PRNT \"GO\"\n");
                s.Append("0019 9447CF;\n");
                s.Append("001C ;      28       BC GOSB \"TO\"\n");
                s.Append("001C 8854CF;\n");
                s.Append("001F ;      29       JS EXPR         GET LINE NUMBER\n");
                s.Append("001F 30BC;  30       BE *\n");
                s
                    .Append("0021 E0;    31       SB              (DO THIS FOR STARTING)\n");
                s.Append("0022 10;    32       RB\n");
                s.Append("0023 11;    33       GO              GO THERE\n");
                s.Append("0024 16;    34 .\n");
                s
                    .Append("0025 ;      35 :GOSB BC * \"SUB\"      NO OTHER WORD BEGINS \"GO...\"\n");
                s.Append("0025 805355C2;\n");
                s.Append("0029 ;      36       JS EXPR\n");
                s.Append("0029 30BC;  37       BE *\n");
                s.Append("002B E0;    38       GS\n");
                s.Append("002C 14;    39       GO\n");
                s.Append("002D 16;    40 .\n");
                s.Append("002E ;      41 :PRNT BC SKIP \"PR\"\n");
                s.Append("002E 9050D2;\n");
                s
                    .Append("0031 ;      42       BC P0 \"INT\"     OPTIONALLY OMIT \"INT\"\n");
                s.Append("0031 83494ED4;\n");
                s.Append("0035 ;      43 :P0   BE P3\n");
                s
                    .Append("0035 E5;    44       BR P6           IF DONE, GO TO END\n");
                s.Append("0036 71;    45 :P1   BC P4 \";\"\n");
                s.Append("0037 88BB;  46 :P2   BE P3\n");
                s
                    .Append("0039 E1;    47       NX              NO CRLF IF ENDED BY ; OR ,\n");
                s.Append("003A 1D;    48 :P3   BC P7 '\"'\n");
                s
                    .Append("003B 8FA2;  49       PQ              QUOTE MARKS STRING\n");
                s
                    .Append("003D 21;    50       BR P1           GO CHECK DELIMITER\n");
                s
                    .Append("003E 58;    51 :SKIP BR IF           (ON THE WAY THRU)\n");
                s.Append("003F 6F;    52 :P4   BC P5 \",\"\n");
                s.Append("0040 83AC;  53       PT              COMMA SPACING\n");
                s.Append("0042 22;    54       BR P2\n");
                s.Append("0043 55;    55 :P5   BC P6 \":\"\n");
                s.Append("0044 83BA;  56       PC \"S^\"         OUTPUT X-OFF\n");
                s.Append("0046 2493;  57 :P6   BE *\n");
                s.Append("0048 E0;    58       NL              THEN CRLF\n");
                s.Append("0049 23;    59       NX\n");
                s
                    .Append("004A 1D;    60 :P7   JS EXPR         TRY FOR AN EXPRESSION\n");
                s.Append("004B 30BC;  61       PN\n");
                s.Append("004D 20;    62       BR P1\n");
                s.Append("004E 48;    63 .\n");
                s.Append("004F ;      64 :IF   BC INPT \"IF\"\n");
                s.Append("004F 9149C6;\n");
                s.Append("0052 ;      65       JS EXPR\n");
                s.Append("0052 30BC;  66       JS RELO\n");
                s.Append("0054 3134;  67       JS EXPR\n");
                s
                    .Append("0056 30BC;  68       BC I1 \"THEN\"    OPTIONAL NOISEWORD\n");
                s.Append("0058 84544845CE;\n");
                s
                    .Append("005D ;      69 :I1   CP              COMPARE SKIPS NEXT IF TRUE\n");
                s.Append("005D 1C;    70       NX              FALSE.\n");
                s
                    .Append("005E 1D;    71       J STMT          TRUE. GO PROCESS STATEMENT\n");
                s.Append("005F 380D;  72 .\n");
                s.Append("0061 ;      73 :INPT BC RETN \"INPUT\"\n");
                s.Append("0061 9A494E5055D4;\n");
                s.Append("0067 ;      74 :I2   BV *            GET VARIABLE\n");
                s.Append("0067 A0;    75       SB              SWAP POINTERS\n");
                s.Append("0068 10;    76       BE I4\n");
                s
                    .Append("0069 E7;    77 :I3   PC \"? Q^\"       LINE IS EMPTY; TYPE PROMPT\n");
                s.Append("006A 243F2091;\n");
                s.Append("006E ;      78       GL              READ INPUT LINE\n");
                s
                    .Append("006E 27;    79       BE I4           DID ANYTHING COME?\n");
                s.Append("006F E1;    80       BR I3           NO, TRY AGAIN\n");
                s.Append("0070 59;    81 :I4   BC I5 \",\"       OPTIONAL COMMA\n");
                s.Append("0071 81AC;  82 :I5   JS EXPR         READ A NUMBER\n");
                s
                    .Append("0073 30BC;  83       SV              STORE INTO VARIABLE\n");
                s.Append("0075 13;    84       RB              SWAP BACK\n");
                s.Append("0076 11;    85       BC I6 \",\"       ANOTHER?\n");
                s.Append("0077 82AC;  86       BR I2           YES IF COMMA\n");
                s.Append("0079 4D;    87 :I6   BE *            OTHERWISE QUIT\n");
                s.Append("007A E0;    88       NX\n");
                s.Append("007B 1D;    89 .\n");
                s.Append("007C ;      90 :RETN BC END \"RETURN\"\n");
                s.Append("007C 895245545552CE;\n");
                s.Append("0083 ;      91       BE *\n");
                s
                    .Append("0083 E0;    92       RS              RECOVER SAVED LINE\n");
                s.Append("0084 15;    93       NX\n");
                s.Append("0085 1D;    94 .\n");
                s.Append("0086 ;      95 :END  BC LIST \"END\"\n");
                s.Append("0086 85454EC4;\n");
                s.Append("008A ;      96       BE *\n");
                s.Append("008A E0;    97       WS\n");
                s.Append("008B 2D;    98 .\n");
                s.Append("008C ;      99 :LIST BC RUN \"LIST\"\n");
                s.Append("008C 984C4953D4;\n");
                s.Append("0091 ;     100       BE L2\n");
                s.Append("0091 EC;   101 :L1   PC \"@^@^@^@^J^@^\" PUNCH LEADER\n");
                s.Append("0092 24000000000A80;\n");
                s.Append("0099 ;     102       LS              LIST\n");
                s.Append("0099 1F;   103       PC \"S^\"         PUNCH X-OFF\n");
                s.Append("009A 2493; 104       NL\n");
                s.Append("009C 23;   105       NX\n");
                s
                    .Append("009D 1D;   106 :L2   JS EXPR         GET A LINE NUMBER\n");
                s.Append("009E 30BC; 107       BE L3\n");
                s.Append("00A0 E1;   108       BR L1\n");
                s
                    .Append("00A1 50;   109 :L3   BC * \",\"        SEPARATED BY COMMAS\n");
                s.Append("00A2 80AC; 110       BR L2\n");
                s.Append("00A4 59;   111 .\n");
                s.Append("00A5 ;     112 :RUN  BC CLER \"RUN\"\n");
                s.Append("00A5 855255CE;\n");
                s.Append("00A9 ;     113       J XEC\n");
                s.Append("00A9 380A; 114 .\n");
                s.Append("00AB ;     115 :CLER BC REM \"CLEAR\"\n");
                s.Append("00AB 86434C4541D2;\n");
                s.Append("00B1 ;     116       MT\n");
                s.Append("00B1 2B;   117 .\n");
                s.Append("00B2 ;     118 :REM  BC DFLT \"REM\"\n");
                s.Append("00B2 845245CD;\n");
                s.Append("00B6 ;     119       NX\n");
                s.Append("00B6 1D;   120 .\n");
                s.Append("00B7 ;     121 :DFLT BV *            NO KEYWORD...\n");
                s.Append("00B7 A0;   122       BC * \"=\"        TRY FOR LET\n");
                s.Append("00B8 80BD; 123       J LET           IT'S A GOOD BET.\n");
                s.Append("00BA 3814; 124 .\n");
                s.Append("00BC ;     125 .  SUBROUTINES\n");
                s.Append("00BC ;     126 .\n");
                s
                    .Append("00BC ;     127 :EXPR BC E0 \"-\"       TRY FOR UNARY MINUS\n");
                s.Append("00BC 85AD; 128       JS TERM         AHA\n");
                s.Append("00BE 30D3; 129       NE\n");
                s.Append("00C0 17;   130       BR E1\n");
                s
                    .Append("00C1 64;   131 :E0   BC E4 \"+\"       IGNORE UNARY PLUS\n");
                s.Append("00C2 81AB; 132 :E4   JS TERM\n");
                s
                    .Append("00C4 30D3; 133 :E1   BC E2 \"+\"       TERMS SEPARATED BY PLUS\n");
                s.Append("00C6 85AB; 134       JS TERM\n");
                s.Append("00C8 30D3; 135       AD\n");
                s.Append("00CA 18;   136       BR E1\n");
                s
                    .Append("00CB 5A;   137 :E2   BC E3 \"-\"       TERMS SEPARATED BY MINUS\n");
                s.Append("00CC 85AD; 138       JS TERM\n");
                s.Append("00CE 30D3; 139       SU\n");
                s.Append("00D0 19;   140       BR E1\n");
                s.Append("00D1 54;   141 :E3   RT\n");
                s.Append("00D2 2F;   142 .\n");
                s.Append("00D3 ;     143 :TERM JS FACT\n");
                s
                    .Append("00D3 30E2; 144 :T0   BC T1 \"*\"       FACTORS SEPARATED BY TIMES\n");
                s.Append("00D5 85AA; 145       JS FACT\n");
                s.Append("00D7 30E2; 146       MP\n");
                s.Append("00D9 1A;   147       BR T0\n");
                s
                    .Append("00DA 5A;   148 :T1   BC T2 \"/\"       FACTORS SEPARATED BY DIVIDE\n");
                s.Append("00DB 85AF; 149       JS  FACT\n");
                s.Append("00DD 30E2; 150       DV\n");
                s.Append("00DF 1B;   151       BR T0\n");
                s.Append("00E0 54;   152 :T2   RT\n");
                s.Append("00E1 2F;   153 .\n");
                s.Append("00E2 ;     154 :FACT BC F0 \"RND\"     *RND FUNCTION*\n");
                s.Append("00E2 97524EC4;\n");
                s
                    .Append("00E6 ;     155       LN 257*128      STACK POINTER FOR STORE\n");
                s.Append("00E6 0A;\n");
                s.Append("00E7 8080; 156       FV              THEN GET RNDM\n");
                s.Append("00E9 12;   157       LN 2345         R:=R*2345+6789\n");
                s.Append("00EA 0A;\n");
                s.Append("00EB 0929; 158       MP\n");
                s.Append("00ED 1A;   159       LN 6789\n");
                s.Append("00EE 0A;\n");
                s.Append("00EF 1A85; 160       AD\n");
                s.Append("00F1 18;   161       SV\n");
                s.Append("00F2 13;   162       LB 128          GET IT AGAIN\n");
                s.Append("00F3 0980; 163       FV\n");
                s.Append("00F5 12;   164       DS\n");
                s.Append("00F6 0B;   165       JS FUNC         GET ARGUMENT\n");
                s.Append("00F7 3130; 166       BR F1\n");
                s.Append("00F9 61;   167 :F0   BR F2           (SKIPPING)\n");
                s.Append("00FA 73;   168 :F1   DS\n");
                s
                    .Append("00FB 0B;   169       SX 2            PUSH TOP INTO STACK\n");
                s.Append("00FC 02;   170       SX 4\n");
                s.Append("00FD 04;   171       SX 2\n");
                s.Append("00FE 02;   172       SX 3\n");
                s.Append("00FF 03;   173       SX 5\n");
                s.Append("0100 05;   174       SX 3\n");
                s
                    .Append("0101 03;   175       DV              PERFORM MOD FUNCTION\n");
                s.Append("0102 1B;   176       MP\n");
                s.Append("0103 1A;   177       SU\n");
                s
                    .Append("0104 19;   178       DS              PERFORM ABS FUNCTION\n");
                s.Append("0105 0B;   179       LB 6\n");
                s.Append("0106 0906; 180       LN 0\n");
                s.Append("0108 0A;\n");
                s.Append("0109 0000; 181       CP              (SKIP IF + OR 0)\n");
                s.Append("010B 1C;   182       NE\n");
                s.Append("010C 17;   183       RT\n");
                s.Append("010D 2F;   184 :F2   BC F3 \"USR\"     *USR FUNCTION*\n");
                s.Append("010E 8F5553D2;\n");
                s
                    .Append("0112 ;     185       BC * \"(\"        3 ARGUMENTS POSSIBLE\n");
                s.Append("0112 80A8; 186       JS EXPR         ONE REQUIRED\n");
                s.Append("0114 30BC; 187       JS ARG\n");
                s.Append("0116 312A; 188       JS ARG\n");
                s.Append("0118 312A; 189       BC * \")\"\n");
                s.Append("011A 80A9; 190       US              GO DO IT\n");
                s.Append("011C 2E;   191       RT\n");
                s.Append("011D 2F;   192 :F3   BV F4           VARIABLE?\n");
                s.Append("011E A2;   193       FV              YES.  GET IT\n");
                s.Append("011F 12;   194       RT\n");
                s.Append("0120 2F;   195 :F4   BN F5           NUMBER?\n");
                s.Append("0121 C1;   196       RT              GOT IT.\n");
                s
                    .Append("0122 2F;   197 :F5   BC * \"(\"        OTHERWISE MUST BE (EXPR)\n");
                s.Append("0123 80A8; 198 :F6   JS EXPR\n");
                s.Append("0125 30BC; 199       BC * \")\"\n");
                s.Append("0127 80A9; 200       RT\n");
                s.Append("0129 2F;   201 .\n");
                s.Append("012A ;     202 :ARG  BC A0 \",\"        COMMA?\n");
                s
                    .Append("012A 83AC; 203       J  EXPR          YES, GET EXPRESSION\n");
                s
                    .Append("012C 38BC; 204 :A0   DS               NO, DUPLICATE STACK TOP\n");
                s.Append("012E 0B;   205       RT\n");
                s.Append("012F 2F;   206 .\n");
                s.Append("0130 ;     207 :FUNC BC * \"(\"\n");
                s.Append("0130 80A8; 208       BR F6\n");
                s.Append("0132 52;   209       RT\n");
                s.Append("0133 2F;   210 .\n");
                s
                    .Append("0134 ;     211 :RELO BC R0 \"=\"        CONVERT RELATION OPERATORS\n");
                s
                    .Append("0134 84BD; 212       LB 2             TO CODE BYTE ON STACK\n");
                s.Append("0136 0902; 213       RT               =\n");
                s.Append("0138 2F;   214 :R0   BC R4 \"<\"\n");
                s.Append("0139 8EBC; 215       BC R1 \"=\"\n");
                s.Append("013B 84BD; 216       LB 3             <=\n");
                s.Append("013D 0903; 217       RT\n");
                s.Append("013F 2F;   218 :R1   BC R3 \">\"\n");
                s.Append("0140 84BE; 219       LB 5             <>\n");
                s.Append("0142 0905; 220       RT\n");
                s.Append("0144 2F;   221 :R3   LB 1             <\n");
                s.Append("0145 0901; 222       RT\n");
                s.Append("0147 2F;   223 :R4   BC * \">\"\n");
                s.Append("0148 80BE; 224       BC R5 \"=\"\n");
                s.Append("014A 84BD; 225       LB 6             >=\n");
                s.Append("014C 0906; 226       RT\n");
                s.Append("014E 2F;   227 :R5   BC R6 \"<\"\n");
                s.Append("014F 84BC; 228       LB 5             ><\n");
                s.Append("0151 0905; 229       RT\n");
                s.Append("0153 2F;   230 :R6   LB 4             >\n");
                s.Append("0154 0904; 231       RT\n");
                s.Append("0156 2F;   232 .\n");
                s.Append("0157 ;    0000\n");
                DEFAULT_IL = s.ToString();
            }
            return DEFAULT_IL;
        } // ~DefaultIL

        //*************************** Startup Code ****************************/

        public void StartTinyBasic(string ILtext)
        {
            int nx;
            for (nx = 0; nx < CoreTop; nx++)
                Core[nx] = 0; // clear Core..
            Poke2(ExpnStk, 8191); // random number seed
            Core[BScode] = 8; // backspace
            Core[CanCode] = 27; //escape
            for (nx = 0; nx < 32; nx++)
                DeCaps[nx] = '\0'; // fill caps table..
            for (nx = 32; nx < 127; nx++)
                DeCaps[nx] = (char)nx;
            for (nx = 65; nx < 91; nx++)
                DeCaps[nx + 32] = (char)nx;
            DeCaps[9] = ' ';
            DeCaps[10] = '\r';
            DeCaps[13] = '\r';
            DeCaps[127] = '\0';
            if (ILtext == null)
                ILtext = DefaultIL(); // no IL given, use mine
            ConvtIL(ILtext); // convert IL assembly code to binary
            ColdStart();
        } // ~StartTinyBasic

        public int RunTinyBasic(int previousReturnCode)
        {
            switch (previousReturnCode)
            {
                case INTERP_NEEDCHAR:
                    ReadChar();
                    break;
                case INTERP_NEEDLINE:
                    ReadLine();
                    break;
            }

            int result = Interp(); // go do it
            if (result == INTERP_DONE)
            {
                if (oFile != null)
                    IoFileClose(oFile); // close output file
                if (inFile != null)
                    IoFileClose(inFile); // close input file
                oFile = null;
                inFile = null;
            }
            return result;
        }
    }
}
