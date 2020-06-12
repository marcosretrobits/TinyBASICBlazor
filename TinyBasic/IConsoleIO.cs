/**
 * Copyright (c) by Mohan Embar 2008. All Rights Reserved.
 * http://www.thisiscool.com/
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace com.thisiscool.tinybasic
{
    public interface IConsoleIO
    {
        void screenChar(char ch);
        void print(string theMsg);
        char read();
    }
}
