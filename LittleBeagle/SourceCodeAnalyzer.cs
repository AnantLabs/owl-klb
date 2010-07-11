using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;

using Owl.Util;

namespace Owl
{
    class CodeTokenizer : TokenStream
    {
        private int offset = 0, bufferIndex = 0, dataLen = 0;
        protected const int MAX_WORD_LEN = 255;
        private const int IO_BUFFER_SIZE = 1024;
        private char[] ioBuffer = new char[IO_BUFFER_SIZE];
        private enum STATES { PASS1, PASS2, PASS3 };
        private STATES  state = STATES.PASS1;
        private bool use_stop_words = false;

        private System.IO.TextReader input;

        private static string[] STOP_WORDS = {
          // Java
            "public","private","protected","interface",
            "abstract","implements","extends","null", "new",
            "switch","case", "default" ,"synchronized" ,
            "do", "if", "else", "break","continue","this",
            "assert" ,"for","instanceof", "transient",
            "final", "static" ,"void","catch","try",
            "throws","throw","class", "finally","return",
            "const" , "native", "super","while", "import",
            "package" ,"true", "false",
          // English
            "a", "an", "and", "are","as","at","be", "but",
            "by", "for", "if", "in", "into", "is", "it",
            "no", "not", "of", "on", "or", "s", "such",
            "that", "the", "their", "then", "there","these",
            "they", "this", "to", "was", "will", "with"
        };


        public CodeTokenizer(System.IO.TextReader reader)            
        {
            input = reader;
        }
        public CodeTokenizer()
        {
            input = null;
        }

        protected virtual bool IsTokenChar(char c)
        {
            return (System.Char.IsLetterOrDigit(c) ||
                (c == '_') ||
                (c == '-') ||
                (c == '.'));
        }

        protected bool IsStopWord(char[] buffer, int start, int len)
        {
            string word_ref = new string(buffer, start, len);
            word_ref = word_ref.ToLower();
            foreach (string word in STOP_WORDS)
            {
                if (System.String.Compare(word, word_ref)==0)
                    return true;
            } 
            return false;    
        }

        //Token m_current = new Token();
        protected char[] current_token = new char[MAX_WORD_LEN];
        protected int current_token_len;
        protected int current_token_index;
        protected int current_token_offset;
        protected virtual bool GetNextToken()
        {
            try
            {
                current_token_len = 0;
                current_token_index = 0;
                while (true)
                {
                    if (bufferIndex >= dataLen)
                    {
                        offset += dataLen;
                        dataLen = input.Read((System.Char[])ioBuffer, 0, ioBuffer.Length);
                        if (dataLen <= 0)
                        {
                            if (current_token_len > 0)
                                break;
                            else
                                return false;
                        }
                        bufferIndex = 0;
                    }

                    char c = ioBuffer[bufferIndex++];

                    if (IsTokenChar(c))
                    {
                        if (current_token_len == 0)
                            current_token_offset = offset + bufferIndex - 1;
                        current_token[current_token_len++] = c;
                        if (current_token_len == MAX_WORD_LEN)
                            // buffer overflow!
                            break;
                    }
                    else if (current_token_len > 0)
                        // at non-Letter w/ chars
                        break; // return 'em
                }
                return true;
            }
            catch (Exception e)
            {
                e = e;
            }
            return false;
        }

        public override Token Next(/* in */ Token reusableToken)
        {
            System.Diagnostics.Debug.Assert(reusableToken != null);

            while (true)
            {
                if (state == STATES.PASS1)
                {
                    while (true)
                    {
                        if (!GetNextToken())
                            return null;    //End of parsing !
                        current_token_index = 0;
                        if (current_token_len >= 3)
                            break;
                    }
                    state = STATES.PASS2;
                }
                if (state == STATES.PASS2)
                {
                    int length = 0;
                    int start = current_token_index;

                    char[] buffer = reusableToken.TermBuffer();
                    bool prevIsCap = false;
                    bool curIsCap = false;
                    bool prevIsLetter = false;
                    bool curIsLetter = false;
                    while (true)
                    {
                        char c = current_token[current_token_index++];

                        prevIsCap = curIsCap;
                        curIsCap = System.Char.IsUpper(c);
                        prevIsLetter = curIsLetter;
                        curIsLetter = System.Char.IsLetter(c);
                        if (length > 0)
                        {
                            if (prevIsCap != curIsCap)
                            {
                                if (prevIsCap)
                                {
                                    if (length > 3)
                                    {
                                        current_token_index--;
                                        break;
                                    }
                                }
                                else
                                {
                                    current_token_index--;
                                    break;
                                }
                            }
                            if (prevIsLetter != curIsLetter)
                            {
                                current_token_index--;
                                break;
                            }
                        }
                        if (curIsLetter)
                        {
                            // if it's a token char
                            if (length == 0)// start of token
                                start = current_token_offset+current_token_index - 1;
                            if (length == buffer.Length)
                            {
                                buffer = reusableToken.ResizeTermBuffer(1 + length);
                            }
                            buffer[length++] = System.Char.ToLower(c);// Normalize(c); // buffer it, normalized
                        }

                        if (length == MAX_WORD_LEN)
                            // buffer overflow!
                            break;

                        if (current_token_index >= current_token_len)
                            break;
                    }
                    if (current_token_index >= current_token_len)
                    {
                        //if i am returning the whole sub token, 
                        //i am not resending it on PHASE3: and directly digging another complete token 
                        //
                        if (length == current_token_len)
                            state = STATES.PASS1;
                        else
                            state = STATES.PASS3;
                    }
                    if ((length >= 3)&&(!use_stop_words||!IsStopWord(buffer, 0, length)))
                    {
                        reusableToken.SetTermLength(length);
                        reusableToken.SetStartOffset(start);
                        reusableToken.SetEndOffset(start + length);
                        return reusableToken;
                    }
                }
                if (state==STATES.PASS3) //sending back the (whole) original token
                {
                    state = STATES.PASS1;
                    if (!use_stop_words||!IsStopWord(current_token, 0, current_token_len))
                    {
                        char[] buffer = reusableToken.TermBuffer();
                        if (current_token_len >= buffer.Length)
                        {
                            buffer = reusableToken.ResizeTermBuffer(current_token_len);
                        }
                        for (int i=0; i<current_token_len; i++)
                        {
                            buffer[i] = System.Char.ToLower(current_token[i]);
                        }
                        reusableToken.SetTermLength(current_token_len);
                        reusableToken.SetStartOffset(current_token_offset);
                        reusableToken.SetEndOffset(current_token_offset + current_token_len);
                        return reusableToken;
                    }
                }
            }
        }
        //
        public override void Reset()
        {
            base.Reset();
            bufferIndex = 0;
            offset = 0;
            dataLen = 0;
            state = STATES.PASS1;
        }
        public virtual void Reset(System.IO.TextReader input)
        {
            base.Reset();
            this.input = input; //base.Reset(input);
        }
                   
        public override void Close()
        {
            if (input != null)
            {
                input.Close();
                input = null;
            }
        }
    }

    /// <summary>An abstract base class for simple, character-oriented tokenizers.</summary>
    public class SourceCodeAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(String fieldName, System.IO.TextReader reader)
        {
            // return new StopFilter(new CodeTokenizer(reader), STOP_WORDS);
            return new CodeTokenizer(reader);
        }
    }
}
