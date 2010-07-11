using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;

using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Threading;

//using FileSystem; //low level ntfs support !
//using FileSystem.Public;

using Lucene.Net.Index;
using IndexWriter = Lucene.Net.Index.IndexWriter;
using StandardAnalyzer = Lucene.Net.Analysis.Standard.StandardAnalyzer;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;



using Owl.Util;


namespace Owl
{
    //defining a callback type
    public delegate void FileProcessorCallback(bool isStart, bool isEnd, string path);

	interface IFileProcessor
	{
		bool Start(out bool shouldClose);
		bool ProcessFile(string path);
		void End(bool shouldClose);
	}

	class LuceneFileProcessor: IFileProcessor
	{
		private bool _is_started;
		private IndexWriter _writer; 
		private int	_nb_indexed;
		private string _index_path;
		private bool _create_index;
		private PerFieldAnalyzerWrapper _default_analyzer;
        private DocumentFactory _doc_factory;
		public LuceneFileProcessor(string indexPath, bool create, DocumentFactory factory) 
		{
			_is_started = false;
			_writer = null;
			_nb_indexed = 0;
			_index_path = indexPath;
			_create_index = create;
			_default_analyzer = null;
            _doc_factory = factory;
		}
		public bool Open()
		{
			_writer = new IndexWriter(_index_path, new StandardAnalyzer(), _create_index);
			_default_analyzer = new PerFieldAnalyzerWrapper(new StandardAnalyzer());
			_default_analyzer.AddAnalyzer("contents", new SourceCodeAnalyzer());
			_default_analyzer.AddAnalyzer("path2", new PathAnalyzer());
            return true;
		}
		public void Close()
		{
			_writer.Close();
			_writer = null;
		}
		public bool Start(out bool shouldClose)
		{			        
			_nb_indexed = 0;
			shouldClose = false;
			if (_writer == null)
			{
				Open();
				shouldClose = (_writer != null);
			}
			return _writer!=null;
		}
		public bool ProcessFile(string filePath)
		{
			long ms = (long)NTFS.InfoFromPath(filePath);
			if (ms >= 0)
			{
                Logger.Log.Info("Indexing " + filePath);
				Document doc = _doc_factory.CreateFromPath(filePath, ms);
                if (doc!=null)
                {
                    _nb_indexed++;
                    _writer.AddDocument(doc, _default_analyzer);
                    if (_nb_indexed % 20 == 1)
                    {
                        _writer.Commit();
                    }
                }
			}
			return true;
		}
		public void End(bool shouldClose)
		{
			_writer.Commit();
			if (shouldClose)
			{
				Close();
			}
		}
	}

    class LuceneFileUpdater : IFileProcessor
    {
        private int _nb_indexed;
        private string _index_path;
        private PerFieldAnalyzerWrapper _default_analyzer = null;
        private DocumentFactory _doc_factory = null;
		private List<BasicFileInfo> _add_file_list = null;
		private List<string> _del_file_list = null;
		private List<BasicFileInfo> _upd_file_list = null;
		private IndexReader _index_reader = null;
		private IndexSearcher _index_searcher = null;
		private JobStatus _job_status = null;
        private bool _is_started = false;

		class BasicFileInfo
		{
			public string FilePath;
			public long LastModification;
			public BasicFileInfo(string path, long lm)
			{
				FilePath = path;
				LastModification = lm;
			}
		}

        public LuceneFileUpdater(string indexPath, DocumentFactory docFactory, JobStatus jobStatus)
        {
            _index_path = indexPath;
            _doc_factory = docFactory;
			_job_status = jobStatus;
        }
        public bool Open()
        {
			bool res = true;
			_nb_indexed = 0;

			_add_file_list = new List<BasicFileInfo>();
			_del_file_list = new List<string>();
			_upd_file_list = new List<BasicFileInfo>();
			_default_analyzer = new PerFieldAnalyzerWrapper(new StandardAnalyzer());
			_default_analyzer.AddAnalyzer("contents", new SourceCodeAnalyzer());
			_default_analyzer.AddAnalyzer("path2", new PathAnalyzer());
			try
			{
				_index_reader = IndexReader.Open(_index_path);
				_index_searcher = new IndexSearcher(_index_reader);
			}
			catch
			{
				res = false;
			}
			return res;
        }

        public bool Start(out bool shouldClose)
        {
            shouldClose = false;
            if (_is_started == true)
                return true;
            shouldClose = true;
            _nb_indexed = 0;
            _is_started = Open();
            return _is_started;
        }
        public bool ProcessFile(string filePath)
        {
            long ms = (long)NTFS.InfoFromPath(filePath);
            if (ms >= 0)
			{
				TermQuery q = new TermQuery(new Term("path", filePath));
				TopDocs hits = _index_searcher.Search(q, 1);
				if (hits.totalHits >= 1)
				{
					Document doc = _index_searcher.Doc(hits.scoreDocs.First().doc);
					System.String mod = doc.Get("modified");
					bool bypass = (mod == DateTools.TimeToString((long)ms, DateTools.Resolution.MINUTE));
					if (!bypass)
					{
						_upd_file_list.Add(new BasicFileInfo(filePath, ms));
					}
				}
				else
				{
					_add_file_list.Add(new BasicFileInfo(filePath, ms));
				}
            }
            return true;
        }
        public void End(bool shouldClose)
        {
            if (!_is_started)
                return;
            if (!shouldClose)
                return;
			//build 2del file list
			if (!_job_status.Cancelled)
			{
				TermEnum term_enum = _index_reader.Terms();
                Term path_term = new Term("path");
                int nb_terms = 0;
				while (term_enum.SkipTo(path_term)) //skip to new term equal or *ABOVE* "path:" !!!
				{
					Term term = term_enum.Term();
                    if (term.Field() != path_term.Field())
                        break;
                    if (!File.Exists(term.Text()))
                    {
                        _del_file_list.Add(term.Text());
                    }
                    if (_job_status.Cancelled) break;
                    nb_terms++;
                }
				term_enum.Close();
                Logger.Log.Info("update: deletion: {0} analyzed terms, found {1} vanished files.", nb_terms, _del_file_list.Count);
			}
			_index_searcher.Close();
			_index_reader.Close();
			//--- deleting deprecated
			if ((_del_file_list.Count > 0) && (!_job_status.Cancelled))
			{
                Stopwatch watch = new Stopwatch();
                watch.Start();

				int num_file = 0;
				int nb_files = _del_file_list.Count;
				IndexWriter writer = new IndexWriter(_index_path, _default_analyzer, false);

				foreach (string path in _del_file_list)
				{
                    if (((num_file++) % 101) == 1)
                    {
                        int progress = ((((num_file++) + 1)) * 100) / nb_files;
                        _job_status.Progress = progress;
                        _job_status.Description = String.Format("upd: removing (from index) file {0}/{1} - {2}", num_file, _del_file_list.Count,
                            StringFu.TimeSpanToString(new TimeSpan((long)(watch.ElapsedMilliseconds)*10000)));
                    }
                    if (_job_status.Cancelled) break;
					writer.DeleteDocuments(new Term("path", path));
				}
				writer.Commit();
				writer.Close();
                watch.Stop();
			}
			//adding new files
			if ((_add_file_list.Count > 0) && (!_job_status.Cancelled))
			{
                Stopwatch watch = new Stopwatch();
                watch.Start();

                IndexWriter writer = null;
                try
                {
                    writer = new IndexWriter(_index_path, _default_analyzer, false, new IndexWriter.MaxFieldLength(IndexWriter.DEFAULT_MAX_FIELD_LENGTH));
                    int num_file = 0;
                    int nb_files = _add_file_list.Count;
                    foreach (BasicFileInfo fi in _add_file_list)
                    {
                        if (((num_file++)%101)==1)
                        {
                            int progress = ((((num_file++) + 1)) * 100) / nb_files;
                            _job_status.Progress = progress;
                            _job_status.Description = String.Format("upd: indexing new file {0}/{1} - {2}", num_file, _add_file_list.Count,
                                StringFu.TimeSpanToString(new TimeSpan((long)(watch.ElapsedMilliseconds) * 10000)));
                        }
                        if (_job_status.Cancelled)
                            break;

                        writer.AddDocument(_doc_factory.CreateFromPath(fi.FilePath, fi.LastModification));
                        if (num_file % 20 == 0)
                        {
                            writer.Commit();
                        }
                    }
                    writer.Commit();
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex);
                }
                finally
                {
                    if (writer != null)
                    {
                        writer.Close();
                        writer = null;
                    }
                }
                watch.Stop();
			}
			//updating modified files
			if ((_upd_file_list.Count > 0) && (!_job_status.Cancelled))
			{
                Stopwatch watch = new Stopwatch();
                watch.Start();

				int num_file = 0;
				int nb_files = _upd_file_list.Count;
                IndexWriter writer = null;
                try
                {
                    writer = new IndexWriter(_index_path, _default_analyzer, false,
                                             new IndexWriter.MaxFieldLength(IndexWriter.DEFAULT_MAX_FIELD_LENGTH));

                    foreach (BasicFileInfo fi in _upd_file_list)
                    {
                        if (((num_file++) % 101) == 1)
                        {
                            int progress = ((((num_file++) + 1)) * 100) / nb_files;
                            _job_status.Progress = progress;
                            _job_status.Description = String.Format("upd: modified file {0}/{1} - {2}", num_file, _upd_file_list.Count,
                                StringFu.TimeSpanToString(new TimeSpan((long)(watch.ElapsedMilliseconds) * 10000)));
                        }
                        if (_job_status.Cancelled) break;
                        writer.UpdateDocument(new Term("path", fi.FilePath),
                            _doc_factory.CreateFromPath(fi.FilePath, fi.LastModification));
                    }
                    writer.Commit();
                    //LittleBeagle.Properties.Settings.Default.NbIndexedFiles = num_file;
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex);
                }
                finally
                {
                    if (writer != null)
                    {
                        writer.Close();
                        writer = null;
                    }
                }
                watch.Stop();
			}
			
        }
    }


    class NtfsFileEnumerator
    {
        public class Params
        {
            public string localDrives;
            public string extensions;
			public IFileProcessor fileProcessor;
            public JobStatus jobStatus;
            public Stopwatch jobWatch;
            //extension separated by, and trimmed
			public Params(string drives, string extensions, IFileProcessor processor)
            {
                this.localDrives = drives;
                this.extensions = extensions;
                this.fileProcessor = processor;
                jobStatus = null;
                jobWatch = null;
            }
        }
        public void DoEnumerate(Object _params)
        {
            Params myparams = (_params as Params);
            string[] ntfs_disks = myparams.localDrives.Split(',');
            string[] extensions = myparams.extensions.Split(',');
            JobStatus status = myparams.jobStatus;
            if (status == null)
                status = new JobStatus("NtfsEnum");
            Stopwatch watch = myparams.jobWatch;
            if (watch==null)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            foreach (string disk_letter in ntfs_disks)
            {
                if (disk_letter.Trim().Length == 0)
                    continue;

                if (status.Cancelled)
                    break;

                Dictionary<UInt64, FileNameAndFrn> result = null;
                NTFS ntfs = new NTFS();

                status.Description = string.Format("listing ntfs journal on {0}...", disk_letter);

                ntfs.Drive = disk_letter;// "D:";
				try
				{
					ntfs.EnumerateVolume(out result, extensions);
                        /*new string[] {".bab" });						
                        { ".h", "*.hpp", "*.inl", "*.c", "*.cpp", ".txt", ".cs", "*.htm*", "*.ini",
						  "*.xml", "*.rtf", "*.doc"
						});*/
				}
				catch (Win32Exception win32_e)
				{
					win32_e = win32_e;
				}
				catch (Exception e)
				{
					e = e;
				}
                //status.Description = string.Format("listed {0} files for processing in {1} on {2}", result.Count, watch.ElapsedMilliseconds / 1000.0, disk_letter);
                int num_file = 0;
				bool should_close;
                myparams.fileProcessor.Start(out should_close);
				if (result != null)
				{
					foreach (KeyValuePair<UInt64, FileNameAndFrn> entry in result)
					{
						if (status.Cancelled)
							break;
						FileNameAndFrn file = (FileNameAndFrn)entry.Value;
						string full_path = ntfs.DirectoryFromFrn(file.ParentFrn) + @"\" + file.Name;
						full_path = full_path.Substring(4);
						myparams.fileProcessor.ProcessFile(full_path);
						num_file++;
						if ((num_file % 101) == 1)
						{
							status.Progress = (num_file * 100) / result.Count;
                            status.Description = string.Format("ntfs on {3} processing file {0}/{1} - {2}",
                                num_file, result.Count, 
                                StringFu.TimeSpanToString(new TimeSpan((long)(watch.ElapsedMilliseconds)*10000)), 
                                disk_letter);
						}
					}
				}
                myparams.fileProcessor.End(should_close);
                string info = string.Format("ntfs file enum: {0} files - {1}", 
                    result.Count, watch.ElapsedMilliseconds / 1000.0);
                Console.WriteLine(info);
                //status.Description = info;
            }

            if (myparams.jobStatus == null)
            {
                status.RemoveFromQueue();
                status = null;
            }
            if (myparams.jobWatch == null)
                watch = null;
        }
    }
	//---
    class StandardFileEnumerator
    {
        public class Params
        {
            public string extensions;
            public string directories;
			public IFileProcessor fileProcessor;
            public JobStatus jobStatus;
            public Stopwatch jobWatch;

			public Params(string dirs, string extensions, IFileProcessor processor)
            {
                this.directories = dirs;
                this.extensions = extensions;
                this.fileProcessor = processor;
            }
        }
        public void DoEnumerate(Object _params)
        {
            Params myparams = (_params as Params);
            string[] dirs = myparams.directories.Split(',');
            string[] extensions = myparams.extensions.Split(',');
            JobStatus status = myparams.jobStatus;
            if (status == null)
                status = new JobStatus("FileStdEnum");
            Stopwatch watch = myparams.jobWatch;
            if (watch == null)
            {
                watch = new Stopwatch();
                watch.Start();
            }
            int num_file = 0;
			bool should_close;
            myparams.fileProcessor.Start(out should_close);
            foreach (string dir in dirs)
            {
                if (status.Cancelled)
                    break;

                if (dir == null) continue;
                if (dir.Trim().Length == 0) continue;
                if (!DirectoryWalker.IsWalkable(dir)) continue;
                //IEnumerable 

	            //foreach (string path in enumerable)

				foreach (FileInfo fi in DirectoryWalker.GetFileInfosRecursive(dir))
				{
                    if (status.Cancelled)
                        break;
					// fe = enumerator as DirectoryWalker.FileEnumerator;
                    string s = Path.GetExtension(fi.Name);
                    foreach (string ext in extensions)
                    {
                        if (string.Compare(s, ext, true)==0)
                        {
                            myparams.fileProcessor.ProcessFile(fi.FullName);
                            break;
                        }
                    }
                    num_file++;
                    if ((num_file % 101) == 1)
                        status.Description = string.Format("std file enum: {0} files - {1}", num_file, watch.ElapsedMilliseconds / 1000.0);

                }
            }
            myparams.fileProcessor.End(should_close);
            if (myparams.jobStatus == null)
            {
                status.RemoveFromQueue();
                status = null;
            }
            if (myparams.jobWatch == null)
                watch = null;
        }
    }


	class MetaFileEnumerator
	{
		public class Params
		{
			public string extensions;
			public string directories;
			public string drives;
			public IFileProcessor fileProcessor;
			public JobStatus jobStatus = null;
            public bool shouldCloseJobStatus = false;
			public Stopwatch jobWatch = null;

			public Params(string drives, string dirs, string extensions, IFileProcessor processor)
			{
				this.directories = dirs;
				this.drives = drives;
				this.extensions = extensions;
				this.fileProcessor = processor;
			}

		}
		public void DoEnumerate(object _params)
		{
			Params myparams = _params as Params;
			JobStatus status = myparams.jobStatus;
			if (status == null)
				status = new JobStatus("MetaFileEnum");
			Stopwatch watch = myparams.jobWatch;
			if (watch == null)
			{
				watch = new Stopwatch();
				watch.Start();
			}
			bool should_close;
            if (myparams.fileProcessor.Start(out should_close))
            {
                if (myparams.drives.Length > 0)
                {
                    NtfsFileEnumerator nfe = new NtfsFileEnumerator();
                    NtfsFileEnumerator.Params nfe_params = new NtfsFileEnumerator.Params(myparams.drives, myparams.extensions, myparams.fileProcessor);
                    nfe_params.jobStatus = status;
                    nfe_params.jobWatch = watch;
                    nfe.DoEnumerate(nfe_params);
                }

                if (myparams.directories.Length > 0)
                {
                    StandardFileEnumerator stdfe = new StandardFileEnumerator();
                    StandardFileEnumerator.Params stdfe_params = new StandardFileEnumerator.Params(myparams.directories, myparams.extensions, myparams.fileProcessor);
                    stdfe_params.jobStatus = status;
                    stdfe_params.jobWatch = watch;
                    stdfe.DoEnumerate(stdfe_params);
                }
                myparams.fileProcessor.End(should_close);
            }
			if ((myparams.jobStatus == null)||(myparams.shouldCloseJobStatus))
			{
				status.RemoveFromQueue();
				status = null;
			}
			if (myparams.jobWatch == null)
				watch = null;
		}
	}

    class DocumentCrawler
    {
		//dispatcher
		//http://blog.nostatic.org/2007/12/wpf-progress-bars.html
        public class BasicFileInfo
        {
            public string   FullPath;
            public Int32    LastModificationMs;
            public BasicFileInfo(string path, Int32 lmms)
            {
                FullPath = path;
                LastModificationMs = lmms;
            }
        };

        Queue<string>        dispatch_queue = null;
        Queue<BasicFileInfo> dispatch_file_queue = null;
        List<BasicFileInfo>  add_file_list = null;
        List<BasicFileInfo>  upd_file_list = null;
        List<BasicFileInfo>  del_file_list = null;
        private Thread      _crawling_thread = null;
        private JobStatus   _crawling_status = null;
        private DocumentFactory _doc_factory = null;



        //this is code dispatcher
        Dispatcher          parent_dispatcher = null;
		private delegate void SimpleDelegate();

        bool                is_updating_index;

        public DocumentCrawler()
        {
            dispatch_file_queue = new Queue<BasicFileInfo>();
        }

        public bool IsCrawling
        {
            get {
                if (_crawling_thread == null)
                    return false;
                return _crawling_thread.IsAlive;
            }

            set {
                if (_crawling_thread == null)
                    return;
                if (value==true)
                    return;
                if (!_crawling_thread.IsAlive)
                    return;
                _crawling_status.Cancelled = true;
                _crawling_thread.Join(2000);
                if (_crawling_thread.IsAlive)
                    _crawling_thread.Abort();
            }
        }
        //
		public void GetParameters(out string extensions, out string drives, out string dirs)
		{
			extensions = ".bab,.c,.cpp,.h,.hpp,.xml,.txt,.ini,.cs,.sh";
            extensions = Owl.Properties.Settings.Default.Extensions; ;
            drives = Owl.Properties.Settings.Default.LocalDrives.Trim();
			dirs = "";
			string dir;
			foreach (System.Configuration.SettingsProperty prop in Owl.Properties.Settings.Default.Properties)
			{
				if (prop.Name.StartsWith("Directory"))
				{
					dir = (string)Owl.Properties.Settings.Default[prop.Name];
					dir = dir.Trim();
					if (dir.Length > 0)
					{
						if (dirs.Length > 0) dirs += ",";
						dirs += dir;
					}
				}
			}
		}
		public void DoUpdateIndex()
		{
            string extensions, drives, dirs;

            GetParameters(out extensions, out drives, out dirs);
            if (_doc_factory == null)
                _doc_factory = new DocumentFactory();
            extensions = _doc_factory.GetSupportedExtensions();

			_crawling_status = new JobStatus("IndexUpdater");
            //open new index
            LuceneFileUpdater lfu = new LuceneFileUpdater(Owl.Properties.Settings.Default.IndexPath, _doc_factory, _crawling_status);

            MetaFileEnumerator mfe = new MetaFileEnumerator();
            MetaFileEnumerator.Params mfe_params = new MetaFileEnumerator.Params(drives, dirs, extensions, lfu);

            mfe_params.jobStatus = _crawling_status;
            mfe_params.shouldCloseJobStatus = true;
            _crawling_thread = new Thread(new ParameterizedThreadStart(mfe.DoEnumerate));
            _crawling_thread.Start(mfe_params); 

		}
        public void DoBuildNewIndex()
        {
			string extensions, drives, dirs;

            if (_doc_factory == null)
                _doc_factory = new DocumentFactory();

            //open new index
			LuceneFileProcessor lfp = new LuceneFileProcessor(Owl.Properties.Settings.Default.IndexPath, true, _doc_factory);

            GetParameters(out extensions, out drives, out dirs);
            extensions = _doc_factory.GetSupportedExtensions();

			MetaFileEnumerator mfe = new MetaFileEnumerator();
            MetaFileEnumerator.Params mfe_params = new MetaFileEnumerator.Params(drives, dirs, extensions, lfp);
            _crawling_status = new JobStatus("NewIndexBuilder");
            mfe_params.jobStatus = _crawling_status;
            mfe_params.shouldCloseJobStatus = true;
            _crawling_thread = new Thread(new ParameterizedThreadStart(mfe.DoEnumerate));
			_crawling_thread.Start(mfe_params); 
        }
        
        IndexWriter writer = null;
        int num_indexed_file = 0;
        void OpenIndex(bool doCreate)
        {
            if (writer == null)
            {
                string index_path = Owl.Properties.Settings.Default.IndexPath;
                bool do_create = doCreate;
			    writer = new IndexWriter(index_path, new StandardAnalyzer(),
									    do_create);//, new IndexWriter.MaxFieldLength(16));
            }
            num_indexed_file = 0;
        }
        void CloseIndex()
        {
            if (writer!=null)
            {
                writer.Close();
                writer = null;
            }
        }


        public void FileProcessor1(bool isStart, bool isFinished, string filePath)
        {
            if (isStart)
            {
                OpenIndex(true);
                return;
            }
            if (isFinished)
            {
			    writer.Commit();
                CloseIndex();
                return;
            }

            UInt64 ms = NTFS.InfoFromPath(filePath);
            if (ms >= 0)
            {
                num_indexed_file++;

                PerFieldAnalyzerWrapper analyzer = new PerFieldAnalyzerWrapper(new StandardAnalyzer());
                analyzer.AddAnalyzer("contents", new SourceCodeAnalyzer());
                analyzer.AddAnalyzer("path2", new PathAnalyzer());

                //SourceCodeAnalyzer    analyzer = new SourceCodeAnalyzer();
                writer.AddDocument(FileDocument.Document(filePath, ms), analyzer);
                
                if (num_indexed_file % 20 == 1)
                {
                    writer.Commit();
                }
            }
            /*
            lock (dispatch_queue)
            {
                if (dispatch_queue == null)
                {
                    dispatch_queue = new Queue<string>();
                }
                dispatch_queue.Enqueue(filePath);
            }
             * */
        }


        public void FileProcessor2(bool isStart, bool isFinished, string filePath)
        {
            if ((isStart)||(isFinished))
                return;

            lock (dispatch_queue)
            {
                if (dispatch_queue == null)
                {
                    dispatch_queue = new Queue<string>();
                }
                dispatch_queue.Enqueue(filePath);
            }
        }
        //AutoResetEvent to synchropnize
        //http://msdn.microsoft.com/en-us/library/system.threading.autoresetevent.aspx
        public void DoDequeue()
        {
            OpenIndex(true);
            JobStatus status = new JobStatus("Dequeue");
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int nb_processed_files = 0;
            while (true)
            {
                bool found = false;
                string path = "";
                int nb_waiting_files = 0;
                lock (dispatch_queue)
                {
                    nb_waiting_files = dispatch_queue.Count;
                    if (nb_waiting_files>0)
                    {
                        path = dispatch_queue.Dequeue();
                        found = true;
                    }
                }
                if (found)
                {
                    this.FileProcessor1(false, false, path);
                    if ((num_indexed_file % 101) == 1)
                    {
                        status.Progress = num_indexed_file * 100 / (num_indexed_file + nb_waiting_files);
                        status.Description = string.Format("{0} indexed, still {1} waiting ({2} s)...", num_indexed_file, nb_waiting_files, watch.ElapsedMilliseconds/1000);
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
            status.RemoveFromQueue();
            writer.Commit();
            CloseIndex();
        }

        //
        //private delegate void SimpleDelegate1();
        /*
        public class CrawlerParams
        {
            public bool     isUpdating;
            public string   localDrives;
            public string   directories;
            public CrawlerParams(bool doUpdate, string drives, string dirs)
            {
                this.isUpdating = doUpdate;
                this.localDrives = drives;
                this.directories = dirs;
            }
        }
        private void _DoCrawl(object _params)
        {
            CrawlerParams crawlerParams = (_params as CrawlerParams);
            bool is_updating_index = crawlerParams.isUpdating;

            string index_path = LittleBeagle.Properties.Settings.Default.IndexPath;

            string[] ntfs_disks = crawlerParams.localDrives.Split(',');
                //LittleBeagle.Properties.Settings.Default.LocalDrives.Split(',');

            string[] dirs = crawlerParams.directories.Split(',');// new string[6];            
            
            dirs[0] = LittleBeagle.Properties.Settings.Default.Directory1;
            dirs[1] = LittleBeagle.Properties.Settings.Default.Directory2;
            dirs[2] = LittleBeagle.Properties.Settings.Default.Directory3;
            dirs[3] = LittleBeagle.Properties.Settings.Default.Directory4;
            dirs[4] = LittleBeagle.Properties.Settings.Default.Directory5;
            dirs[5] = LittleBeagle.Properties.Settings.Default.Directory6;
            

            JobStatus job = new JobStatus("Crawler2");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            //JobQueue job = new JobQueue("crawling");
            //SimpleDelegate del1 = delegate() { jobItems.Add(job); };
            //parent_dispatcher.Invoke(DispatcherPriority.Send, del1);

            int nb_crawled_files = 0;

            IndexReader index_reader = null;
            IndexSearcher index_searcher = null;
            
            if (is_updating_index)
            {
                try
                {
                    index_reader = IndexReader.Open(index_path);
                    index_searcher = new IndexSearcher(index_reader);
                }
                catch
                {
                    is_updating_index = false;
                }
            }

            //IndexSearcher index_searcher = new IndexSearcher(index_path);

            
            //////////////////////////////////////////////////////////////////////////
            //basic crawling using system
            int num_file2 = 0;
            foreach (string dir in dirs)
			{
                if (job.Cancelled)
                    break;

				if (dir == null) continue;
				if (dir.Trim().Length == 0) continue;
                if (!DirectoryWalker.IsWalkable(dir)) continue;
                IEnumerable enumerator = DirectoryWalker.GetItemsRecursive(dir, null);
				foreach (string path in enumerator)
                {
                    if (job.Cancelled)
                        break;

                    if (IsCandidate(path))
                    {
                        lock (dispatch_file_queue)
                        {
                            //dispatch_file_queue.Enqueue(
                            //    new BasicFileInfo(path, (enumerator as DirectoryWalker.FileEnumerator).CurLastWriteTimeInMs())
                            //);
                        }
                    }
                    num_file2++;
                    if ((num_file2%101)==1)
                        job.Description = string.Format("std file enum: {0} files - {1}", num_file2, stopWatch.ElapsedMilliseconds / 1000.0);

                }
            }
            job.RemoveFromQueue();
            job = null;
        }
         */
    }
}
