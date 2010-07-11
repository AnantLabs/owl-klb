using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;

using Owl.Util;

namespace Owl
{
    public class PathTokenizer: Tokenizer
    {
        private string  workingString;
        private bool firstTime = true;
        private int currentIndex, currentTokenIndex;
        public PathTokenizer(System.IO.TextReader reader)
            : base(reader)
        {
            firstTime = true;
        }
        public override Token Next(/* in */ Token reusableToken)
        {
            System.Diagnostics.Debug.Assert(reusableToken != null);

            if (firstTime)
            {
                workingString = input.ReadToEnd();
                currentIndex = 0;
                currentTokenIndex = 0;
                firstTime = false;
                /*
                reusableToken.SetStartOffset(0);
                reusableToken.SetEndOffset(workingString.Length);
                reusableToken.SetTermText(workingString);
                */
                workingString = workingString.Replace('/', '\\').ToLower();

                return reusableToken;                
            }
            while (true)
            {
                if (currentIndex == workingString.Length)
                    return null; //ended !

                int token_len = 0;
                while (currentIndex < workingString.Length)
                {
                    int c = workingString[currentIndex++];
                    if ((c == '\\') || (c == '/'))
                        break;
                    token_len++;
                }
                int token_start = currentTokenIndex;
                currentTokenIndex = currentIndex;
                if (token_len > 1)
                {
                    //reusableToken.SetTermLength(token_len);
                    reusableToken.SetStartOffset(token_start);
                    reusableToken.SetEndOffset(token_start + token_len);
                    reusableToken.SetTermText(workingString.Substring(token_start, token_len));
                    return reusableToken;
                }
            }            
            //reusableToken.SetStartOffset(start);
            //reusableToken.SetEndOffset(start + length);
            return null;
        }

        public override void Reset(System.IO.TextReader input)
        {
            base.Reset(input);
            firstTime = true;
        }
    }

    /// <summary> "Tokenizes" the entire stream as a single token. This is useful
    /// for data like zip codes, ids, and some product names.
    /// </summary>
    public class PathAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(System.String fieldName, System.IO.TextReader reader)
        {
            return new PathTokenizer(reader);
        }

        public override TokenStream ReusableTokenStream(System.String fieldName, System.IO.TextReader reader)
        {
            Tokenizer tokenizer = (Tokenizer)GetPreviousTokenStream();
            if (tokenizer == null)
            {
                tokenizer = new PathTokenizer(reader);
                SetPreviousTokenStream(tokenizer);
            }
            else
                tokenizer.Reset(reader);
            return tokenizer;
        }
    }
}
