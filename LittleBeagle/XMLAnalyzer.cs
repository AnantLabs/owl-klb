using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using com.ximpleware;

using Owl.Util;


namespace Owl
{
    /*
    class XMLTokenizer : CharTokenizer
    {

    }

    class XMLAnalyser: Analyzer
    {
        public override TokenStream TokenStream(String fieldName, System.IO.TextReader reader)
        {
            // return new StopFilter(new CodeTokenizer(reader), STOP_WORDS);
            return new XMLTokenizer(reader);
        }
    }
     * */
    class XMLTokenStream : CodeTokenizer
    {
        string    _fullname;
        bool      _isfirsttime;
        VTDGen    _vg;
        VTDNav    _vgnav;
        //AutoPilot _vgap;
        int _current_index;

        protected override bool GetNextToken()
        {
            if (_isfirsttime)
            {
                _vgnav = null;
                _vg = new VTDGen();

				if (_vg.parseFile(_fullname, true))
                {
					_vgnav = _vg.getNav();
					_vgnav.toElement(VTDNav.ROOT);
                        //_vgap = new AutoPilot(_vgnav);
                        //_vgap.selectElement("*");
                    _isfirsttime = false;
                    _current_index = 0;
                }               
            }
            if (_vgnav == null)
                return false;
            int nb_tokens = _vgnav.getTokenCount();
            current_token_len = 0;
            while (true)
            {
                try
                {
                    if (_current_index >= nb_tokens)
                        return false;
                    int len = _vgnav.getTokenLength(_current_index);
                    if (len >= 3)
                    {
                        string token = _vgnav.toString(_current_index);
                        len = token.Length;
                        if (len >= 3)
                        {
                            if (len > MAX_WORD_LEN)
                                len = MAX_WORD_LEN;
                            current_token = token.ToCharArray(0, len);
                            current_token_offset = _vgnav.getTokenOffset(_current_index);
                            current_token_len = len; //_vgnav.getTokenLength(_current_index)
                            _current_index++;
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    e = e;
                    return false;
                }
                
                _current_index++;
            }
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            _isfirsttime = true;
            _vgnav = null;
        }

        public override void Close()
        {
            base.Close();
            _vgnav = null;
            _isfirsttime = true;
        }
        //////////////////////////////////////////////////////////////////////////
        public XMLTokenStream(string fullname):base()
        {
            _fullname = fullname;
            _isfirsttime = true;
        }
    }
}
