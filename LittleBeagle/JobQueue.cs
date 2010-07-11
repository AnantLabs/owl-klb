using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Owl
{
    /// <summary>
    /// 
    /// </summary>
    public class JobQueue
    {
        public ObservableCollection<JobStatus> jobStatusList = new ObservableCollection<JobStatus>();
        /*
        public JobStatus Create(string identifier)
        {
            JobStatus status = new JobStatus(identifier);
            lock (status)
            {
                int index = jobStatusList.IndexOf(status);
                if (index == -1)
                {
                    index = jobStatusList.Count;
                    jobStatusList.Add(status);
                }
                return jobStatusList[index];
            }
        }
        public void Destroy(JobStatus status)
        {
            lock (status)
            {
                jobStatusList.Remove(status);
            }
        }
         * */
    }



    public class JobStatus : INotifyPropertyChanged
    {
		private string  name;
        private string  description;
        private int     progress;
        private bool    cancelled;
        public event PropertyChangedEventHandler PropertyChanged;

        public override int GetHashCode()
        {
            return name.GetHashCode();
        }
        public override bool Equals(Object obj) { return (obj as JobStatus).name == name; }
        //override public bool Equals(JobStatus obj1, JobStatus obj2) { return obj1.name == obj2.name; }

        #region Properties Getters and Setters
        public string Name
        {
            get { return this.name; }
            set
            {
                this.name = value;
            }
        }
        public string Description
        {
            get { return this.description; }
            set
            {
                this.description = value;
                OnPropertyChanged("Description");
            }
        }
        public int Progress
        {
            get { return this.progress; }
            set
            {
                this.progress = value;
                OnPropertyChanged("Progress");
            }
        }	

        public bool Cancelled
        {
            get { return this.cancelled; }
            set	
            {
                this.cancelled = value;
                OnPropertyChanged("Cancelled");
            }
        }
       
        #endregion

        public JobStatus(string name)
        {
            this.progress = -1;
            this.description = "";
            this.name = name;
            this.cancelled = false;

            AddtoQueue();
        }

        ~JobStatus()
        {
        }
        
        //ensure it will be done in the correct UI thread
        private delegate void SimpleDelegate();
        void AddtoQueue()
        {
            Application app = System.Windows.Application.Current;
            if (app != null)
            {
                SimpleDelegate del = delegate() { 
                    (app as Owl.App).JobItems.Add(this); 
                };
                app.Dispatcher.BeginInvoke(
                    DispatcherPriority.Send, 
                    del
                );
            }
            //this.Dispatcher.Invoke(DispatcherPriority.Send, del1);
        }
        public void RemoveFromQueue()
        {
            Application app = System.Windows.Application.Current;
            if (app != null)
            {
                SimpleDelegate del = delegate() { (app as Owl.App).jobQueue.jobStatusList.Remove(this); };
                app.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    del
                );
            }
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
        
    }

    /*
    class Callback
    {
        ObservableCollection<Message> messages = new ObservableCollection<Message>();
         public void ReceiveMessage(Message message)
        {
            Application app = System.Windows.Application.Current;
            if (app != null)
                app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(Add), message);
        }
        private object Add(object str)
        {
            messages.Add((Message)str);
            return null;
        }
        public IList<Message> Messages
        {
            get { return messages; }
        }
    }
    */


}
