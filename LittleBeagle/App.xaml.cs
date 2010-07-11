using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

using System.Collections.ObjectModel;
using Owl.Util;
using System.Threading;
using System.Windows.Threading;
using System.ComponentModel;
using System.IO;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using System.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows.Input;

//using FileSystem; //strong ntfs support !
//using FileSystem.Public;


namespace Owl
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{        
        private Thread crawlerThread = null;
        private DocumentCrawler _document_crawler = null;

        private ObservableCollection<Result> resultItems = new ObservableCollection<Result>();
        public  JobQueue jobQueue = new JobQueue();
		private Status status = new Status();


        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Console.Out.WriteLine("OWL Log in: "+System.Environment.GetFolderPath(Environment.SpecialFolder.Personal));
            Log.Initialize(System.Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Owl", LogLevel.Debug, true);			
			Logger.Log.Info("Application Startup");
			Infos.LogPath = Log.LogPath;
            //clearing lock (if app has been abruptly put down)
            Lucene.Net.Store.FSDirectory d = Lucene.Net.Store.FSDirectory.GetDirectory(Owl.Properties.Settings.Default.IndexPath);
            if (d.FileExists(IndexWriter.WRITE_LOCK_NAME))
            {
                Logger.Log.Info("Owl last indexing has been abruptly shut down: now clearing lock.");
                d.ClearLock(IndexWriter.WRITE_LOCK_NAME);
            }
        }

        void AppStartup(object sender, StartupEventArgs args)
        {
            /*
            string index_path = LittleBeagle.Properties.Settings.Default.IndexPath;
            IndexReader reader = IndexReader.Open(index_path);
            int nb_docs = reader.NumDocs();
            reader.Close();
            status.SearchStatus = string.Format("{0} documents indexed", nb_docs);
             */
            //---
        }
        /*
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo dirInfo in allDrives)
            {
                Console.WriteLine("Drive {0}", dirInfo.Name);
                Console.WriteLine("File type: {0}", dirInfo.DriveType);
                if (dirInfo.IsReady == true)
                {
                    Console.WriteLine("Volume label: {0}", dirInfo.VolumeLabel);
                    Console.WriteLine("File system: {0}", dirInfo.DriveFormat);
                    Console.WriteLine("Available space:{0, 15} bytes", dirInfo.AvailableFreeSpace);

                    Console.WriteLine("Total available space: {0, 15} bytes", dirInfo.TotalFreeSpace);

                    Console.WriteLine("Total size:{0, 15} bytes ", dirInfo.TotalSize);
                    Console.WriteLine("\n");
                }
            }

            */        
		public ObservableCollection<Result> ResultItems
		{
			get { return this.resultItems; }
			//set { this.resultItems = value; }
		}
	
		public ObservableCollection<JobStatus>  JobItems
		{
			get { return this.jobQueue.jobStatusList; }
            //set { this.jobQueue.jobStatusList = value; }
		} 
        
		public Status Infos
		{
			get { return status; }
		}

        Thread searcher = null;
        string _request;
        static ManualResetEvent _mre = new ManualResetEvent(false);
        
		public void Search(string request)	
		{
            if ((searcher == null)||(!searcher.IsAlive))
            {
                searcher = new Thread(new ThreadStart(_Search));
                _request = "";
                searcher.Start();
                searcher.IsBackground = true;
            }
            lock(_request)
            {
                _request = request;
            }
            _mre.Set();
        }

        bool HasRequestChanged(string reference)
        {
            lock (_request)
            {
                return !_request.Equals(reference);
            }
        }
        delegate void SimpleCall();
        public void _Search()
        {
            //string request = (searchParams as string);
            string old_request = "";
            string new_request = "";

            while (true)
            {
                lock (_request)
                {
                    new_request = _request;
                }

                if (new_request != old_request)
                {
                    old_request = new_request;
                    if (new_request.Length != 0)
                    {
                        IndexReader reader = null;
                        Stopwatch stopWatch = new Stopwatch();
                        stopWatch.Start();

                        string index_path = Owl.Properties.Settings.Default.IndexPath;
                        try
                        {
                            reader = IndexReader.Open(index_path);
                        }
                        catch
                        {
                            status.SearchStatus = string.Format("Problems while opening Index: has it been created in {0} ?", Owl.Properties.Settings.Default.IndexPath);
                        }
                        int nb_docs = 0;
                        int found_docs = 0;
                        if (reader != null)
                        {
                            try
                            {
                                Searcher searcher = new IndexSearcher(reader);
                                Analyzer analyzer = new StandardAnalyzer();
                                //QueryParser parser = new QueryParser("contents", analyzer);
                                MultiFieldQueryParser parser = new MultiFieldQueryParser(new string[] { "contents", "path" }, analyzer);

                                Query query = parser.Parse(new_request);

                                SimpleCall sc = delegate() { resultItems.Clear(); };
                                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, sc);

                                //Hits hits = searcher.Search(query);
                                TopDocs docs = searcher.Search(query, null, 100);
                                int num_doc = 1;
                                foreach (ScoreDoc score_doc in docs.scoreDocs)
                                {
                                    if (HasRequestChanged(new_request))
                                        break;

                                    Document doc = searcher.Doc(score_doc.doc);
                                    System.String path = doc.Get("path");
                                    //
                                    SimpleCall sc2 = delegate()
                                    {
                                        resultItems.Add(new Result(string.Format("{0} - {2} ({1})%\n{3}",
                                         num_doc++, (int)((score_doc.score * 100) / docs.GetMaxScore()),
                                         System.IO.Path.GetFileName(path), System.IO.Path.GetDirectoryName(path)), path));
                                    };
                                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, sc2);

                                    //
                                }
                                found_docs = docs.scoreDocs.Length;
                                nb_docs = reader.NumDocs();
                                searcher.Close();
                            }
                            //catch (TokenMgrError)
                            //{ }
                            catch (Exception e)
                            {
                                status.SearchStatus = string.Format("Problems with request {0} ", new_request);
                                Log.Error(e);
                            }
                            finally
                            {
                                reader.Close();
                                stopWatch.Stop();
                            }
                        }
                        //---
                        status.SearchStatus = string.Format("{0} results for '{3}' in {1} docs (took {2} ms)", found_docs,
                            nb_docs, stopWatch.ElapsedMilliseconds, new_request);

                    }
                }
                else
                {
                    _mre.Reset();
                    _mre.WaitOne();
                }
                //Thread.Sleep(250);
            }
		}
		
        public void StopAllJobs()
        {
			foreach (JobStatus job in JobItems)
				job.Cancelled = true;
            if (crawlerThread != null)
            {
                if (crawlerThread.IsAlive)
                {
					crawlerThread.Join(2000);
					if (crawlerThread.IsAlive)
						crawlerThread.Abort();
				}
                crawlerThread = null;
            }
        }
       
        /// Index scanning

		//dispatcher
		//http://blog.nostatic.org/2007/12/wpf-progress-bars.html
		/// <summary>
		/// Index Builder
		/// </summary>
		public void BuildIndex(bool isUpdating)
		{
            if (_document_crawler == null)
            {
                _document_crawler = new DocumentCrawler();
            }
            else
            {
                if (_document_crawler.IsCrawling)
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Do you want to stop crawler currently running ?",
                        "Owl",
                        MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.No)
                        return;
                    _document_crawler.IsCrawling = true;
                }
            }
            //Put the block of code to be executed as parameter to ThreadStart delegate
            if (isUpdating)
                _document_crawler.DoUpdateIndex();
            else
                _document_crawler.DoBuildNewIndex();
        }


        public void SkipAllJob_Click(object sender, RoutedEventArgs e)
        {
            //JobList.CurrentItem;//((Button)sender).DataContext = 
            Object test = ((System.Windows.Controls.Button)sender).DataContext;
            if (test != null)
            {
                (test as JobStatus).Cancelled = true;
				//(test as JobQueue).worker.CancelAsync();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            StopAllJobs();
            Owl.Properties.Settings.Default.Save();
        }

		public void OnResultClick(object sender, RoutedEventArgs e)
		{
			Object test = ((System.Windows.Controls.Button)sender).DataContext;
			if (test != null)
			{
				//System.IO.FileInfo io = new System.IO.FileInfo((test as Result).Path);

				// suppose that we have a test.txt at E:\
				string filePath = (test as Result).Path;
				if (!File.Exists(filePath))
				{
					return;
				}

				// combine the arguments together
				// it doesn't matter if there is a space after ','
				string argument = @"/select, " + filePath;

				System.Diagnostics.Process.Start("explorer.exe", argument);

			}
		}

        public static ExploreCommand MyExploreCommand = new ExploreCommand();

        public class ExploreCommand : ICommand
        {
            // Summary:
            //     Defines the method that determines whether the command can execute in its
            //     current state.
            //
            // Parameters:
            //   parameter:
            //     Data used by the command. If the command does not require data to be passed,
            //     this object can be set to null.
            //
            // Returns:
            //     true if this command can be executed; otherwise, false.
            public bool CanExecute(object parameter)
            {
                if (parameter == null)
                    return false;
                if (!(parameter is string))
                    return false;
                string path = parameter as string;
                bool res = false;
                if (path.EndsWith("\\"))
                    res = System.IO.Directory.Exists(path);
                else
                    res = System.IO.File.Exists(path);
                return res;
            }
            //
            // Summary:
            //     Defines the method to be called when the command is invoked.
            //
            // Parameters:
            //   parameter:
            //     Data used by the command. If the command does not require data to be passed,
            //     this object can be set to null.
            public void Execute(object parameter)
            {
                // combine the arguments together
                // it doesn't matter if there is a space after ','
                string argument = @"/select, " + (parameter as string);
                System.Diagnostics.Process.Start("explorer.exe", argument);
            }

            // Summary:
            //     Occurs when changes occur that affect whether or not the command should execute.
            public event EventHandler CanExecuteChanged = delegate { };


        }
    }


	public class Status : INotifyPropertyChanged
	{
		private string searchStatus;
		private string logPath;
		public event PropertyChangedEventHandler PropertyChanged;
		
		private static readonly PropertyChangedEventArgs _searchStatusChangedEventArgs
			= new PropertyChangedEventArgs("SearchStatus");
		private static readonly PropertyChangedEventArgs _logPathChangedEventArgs
			= new PropertyChangedEventArgs("LogPath");

		public string SearchStatus
		{
			get { return this.searchStatus; }
			set { this.searchStatus = value; RaisePropertyChanged(_searchStatusChangedEventArgs); }
		}
		public string LogPath
		{
			get { return this.logPath; }
			set { this.logPath = value; RaisePropertyChanged(_logPathChangedEventArgs); }
		}

		private void RaisePropertyChanged(PropertyChangedEventArgs eventArgs)
		{
			if (PropertyChanged == null) return;
			PropertyChanged(this, eventArgs);
		}
	}

}