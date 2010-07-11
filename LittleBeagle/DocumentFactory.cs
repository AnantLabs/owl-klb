using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DateTools = Lucene.Net.Documents.DateTools;
using Document = Lucene.Net.Documents.Document;
using Field = Lucene.Net.Documents.Field;

namespace Owl
{
	interface IDocumentIndexer
	{
		string ID();
		string SupportedExts();
		Document CreateLuceneDocFromPath(string path, long ms);
	}

	class DocumentFactory
	{
		Dictionary<string, IDocumentIndexer> _dic_indexer;
		TextIndexer _default_indexer = new TextIndexer();
		
		public DocumentFactory()
		{
			_dic_indexer = new Dictionary<string, IDocumentIndexer>();
            RegisterIndexer(new TextIndexer());
            RegisterIndexer(new SourceIndexer());
            RegisterIndexer(new XmlIndexer());
        }
		public void RegisterIndexer(IDocumentIndexer docIndexer)
		{
			string[] exts = docIndexer.SupportedExts().Split(',');
			foreach(string cur_ext in exts)
			{
				string ext = cur_ext.Trim().ToLower();
				if (ext.Length>0)
					_dic_indexer.Add(ext, docIndexer);
			}

		}
		public Document CreateFromPath(string path, long ms)
		{
			IDocumentIndexer indexer = null;
			if (!_dic_indexer.TryGetValue(System.IO.Path.GetExtension(path).ToLower(), out indexer))
				indexer = _default_indexer;
			return indexer.CreateLuceneDocFromPath(path, ms);
		}
        public string GetSupportedExtensions()
        {
            string exts = "";
            foreach (string key in _dic_indexer.Keys)
            {
                exts += key+",";
            }
            if (exts.Length>0)
                exts = exts.Remove(exts.Length-1);
            return exts;
        }
	}
	//this is tandard text globbing, everything is tok in charge by default tokenizers
	class TextIndexer: IDocumentIndexer
	{
		public string ID() { return "TextIndexer V1.0.0";  }
		public string SupportedExts() { return ".txt,.text,.ini,.nfo";  }
		public Document CreateLuceneDocFromPath(string fullName, long lastWriteTimeInMs)
		{
			// make a new, empty document
			Document doc = new Document();
			// Add the path of the file as a field named "path".  Use a field that is 
			// indexed (i.e. searchable), but don't tokenize the field into words.
			doc.Add(new Field("path", fullName, Field.Store.YES, Field.Index.NOT_ANALYZED));
            // Add the last modified date of the file a field named "modified".  Use 
			// a field that is indexed (i.e. searchable), but don't tokenize the field
			// into words.
			doc.Add(new Field("modified", DateTools.TimeToString(lastWriteTimeInMs, DateTools.Resolution.MINUTE), Field.Store.YES, Field.Index.NOT_ANALYZED));
            
            doc.Add(new Field("path2", fullName, Field.Store.YES, Field.Index.ANALYZED));
            // Add the contents of the file to a field named "contents".  Specify a Reader,
			// so that the text of the file is tokenized and indexed, but not stored.
			// Note that FileReader expects the file to be in the system's default encoding.
			// If that's not the case searching for special characters will fail.
			try
			{
				System.IO.StreamReader io = new System.IO.StreamReader(fullName, System.Text.Encoding.Default);
				doc.Add(new Field("contents", io));
			}
			catch (System.IO.IOException e)
			{

			}
			// return the document
			return doc;
		}
	}

	class SourceIndexer : IDocumentIndexer
	{
		public string ID() { return "SourceIndexer V1.0.0"; }
		public string SupportedExts() { return ".c,.h,.cpp,.hpp,.cs,.java"; }
		public Document CreateLuceneDocFromPath(string fullName, long lastWriteTimeInMs)
		{
			Document doc = new Document();
			doc.Add(new Field("path", fullName, Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("modified", DateTools.TimeToString(lastWriteTimeInMs, DateTools.Resolution.MINUTE), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("path2", fullName, Field.Store.YES, Field.Index.ANALYZED));
            try
			{
				System.IO.StreamReader io = new System.IO.StreamReader(fullName, System.Text.Encoding.Default);
				doc.Add(new Field("contents", new CodeTokenizer(io)));
			}
			catch (System.IO.IOException e)
			{
			}
			return doc;
		}
	}

	class XmlIndexer : IDocumentIndexer
	{
		public string ID() { return "XmlIndexer V1.0.0"; }
		public string SupportedExts() { return ".bab,.xml,.html"; }
		public Document CreateLuceneDocFromPath(string fullName, long lastWriteTimeInMs)
		{
			Document doc = new Document();
			doc.Add(new Field("path", fullName, Field.Store.YES, Field.Index.NOT_ANALYZED));
			doc.Add(new Field("modified", DateTools.TimeToString(lastWriteTimeInMs, DateTools.Resolution.MINUTE), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("path2", fullName, Field.Store.YES, Field.Index.ANALYZED));
            try
			{
				doc.Add(new Field("contents", new XMLTokenStream(fullName)));
			}
			catch (System.IO.IOException e)
			{
			}
			return doc;
		}
	}

}
