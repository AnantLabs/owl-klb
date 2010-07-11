using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace Owl.Util
{
    class DirectoryWalker
    {
        //http://www.pinvoke.net/default.aspx/kernel32/FindFirstFile.html
   
		public  delegate bool   FileFilter      (string path, string name);
		public  delegate object FileObjectifier (string path, string name);

        public const int MAX_PATH = 260;
        public const int MAX_ALTERNATE = 14;

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public int nFileSizeHigh;
            public int nFileSizeLow;
            public int dwReserved0;
            public int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_ALTERNATE)]
            public string cAlternate;
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        public static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FindClose(IntPtr hFindFile);

        private static Encoding filename_encoding = Encoding.Default;

        public class DirInfo
        {
            public WIN32_FIND_DATA findData;
            public string directory;
            public IntPtr findHandle;
            public bool lastResult;
            public DirInfo()
            {
                lastResult = false;
                findHandle = (IntPtr)(-1);
            }
            public DirInfo(string path)
            {
                findHandle = FindFirstFile(@"\\?\" + path, out findData);
                lastResult = (findHandle != (IntPtr)(-1));
            }
            ~DirInfo()
            {
                if (findHandle != (IntPtr)(-1))
                {
                    FindClose(findHandle);
                    findHandle = (IntPtr)(-1);
                }
            }
        }

        private static DirInfo opendir(string directory)
        {
            DirInfo dirinfo = new DirInfo();
            
            // please note that the following line won't work if you try this on a network folder, like \\Machine\C$
            // simply remove the \\?\ part in this case or use \\?\UNC\ prefix
            // directory ending with a \\ is perfectly valid at least for root dir like c:\\
            string mask = @"\\?\" + directory + (directory.EndsWith(@"\") ? "*" : @"\*");
            dirinfo.findHandle = FindFirstFile(mask, out dirinfo.findData);
            dirinfo.directory = directory;
            dirinfo.lastResult = (dirinfo.findHandle != (IntPtr)(-1));
            //IntPtr test;
            //Marshal.StructureToPtr(dirinfo, test, false);
            return dirinfo;
        }

        private static void closedir (DirInfo dirinfo)
        {
            if (dirinfo != null)
            {
                if (dirinfo.findHandle != (IntPtr)(-1))
                    FindClose(dirinfo.findHandle);
                dirinfo.findHandle = (IntPtr)(-1);
                dirinfo.lastResult = false;
                dirinfo = null;
            }
        }

		private static string readdir (DirInfo dirinfo, ref byte[] buffer)
		{
            if (dirinfo==null)
                return null;
            //if (dir == IntPtr(INVALID_HANDLE_VALUE))
            if (!dirinfo.lastResult)
                return null;
            string res = dirinfo.directory + (dirinfo.directory.EndsWith(@"\") ? "" : @"\") +
                            dirinfo.findData.cFileName;
            /*
            int r = 0;
            
			// We can reuse the same buffer since sys_readdir
			// will fill up the rest of the space by null characters
			r = sys_readdir (dir, buffer, buffer.Length); 
			if (r == -1)
				return null;

			int n_chars = 0;
			while (n_chars < buffer.Length && buffer [n_chars] != 0)
				++n_chars;

			return FileNameMarshaler.LocalToUTF8 (buffer, 0, n_chars);

            while (Kernel32.FindNextFile(findHandle, out findData)) ;
             * */
            dirinfo.lastResult = FindNextFile(dirinfo.findHandle, out dirinfo.findData);

            return res;
        }

		public class FileEnumerator : IEnumerator {			
			string      path;
			FileFilter  file_filter;
			FileObjectifier file_objectifier;
			DirInfo     dir_handle;// = IntPtr.Zero;
			string      current;
			byte[] buffer = new byte [256];

			public bool NamesOnly = false;
            public Int32 CurLastWriteTimeInMs()
            {
                FILETIME ft = dir_handle.findData.ftLastWriteTime;
                return System.DateTime.FromFileTime((((long)ft.dwHighDateTime) << 32) | ft.dwLowDateTime).Millisecond;
            }
			public bool CurIsDirectory()
			{
				return (dir_handle.findData.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory;
			}
			
			public FileEnumerator (string          path,
					       FileFilter      file_filter,
					       FileObjectifier file_objectifier)
			{
				this.path = path;
				this.file_filter = file_filter;
				this.file_objectifier = file_objectifier;
				Reset ();
			}
			
			~FileEnumerator ()
			{
				closedir (dir_handle);
			}

			public object Current {
				get { 
					object current_obj = null;
					if (current != null) {
                        if (file_objectifier != null)
                            current_obj = file_objectifier(path, current);
                        else if (NamesOnly)
                            current_obj = current;
                        else
                            current_obj = Path.Combine(path, current);
					}

					return current_obj;
				}
			}

			public bool MoveNext ()
			{
				bool skip_file = false;

				do {
					current = readdir (dir_handle, ref buffer);
					if (current == null)
						break;

					skip_file = false;

					if (current == "." || current == "..") {
						skip_file = true;

					} else if (file_filter != null) {
						try {
							if (! file_filter (path, current))
								skip_file = true;

						} catch (Exception ex) {
							Logger.Log.Debug (ex, "Caught exception in file_filter");

							// If we have a filter that fails on a file,
							// it is probably safest to skip that file.
							skip_file = true;
						}
					}

				} while (skip_file);

				if (current == null) {
					closedir (dir_handle);
					//dir_handle = IntPtr.Zero;
				}

				return current != null;
			}

			public void Reset ()
			{
				current = null;
				//if (dir_handle != IntPtr.Zero)
				closedir (dir_handle);
				dir_handle = opendir (path);
				if (!dir_handle.lastResult)
					throw new DirectoryNotFoundException (path);
			}
		}

		private class FileEnumerable : IEnumerable {

			string path;
			FileFilter file_filter;
			FileObjectifier file_objectifier;
			
			public bool NamesOnly = false;

			public FileEnumerable (string          path,
					       FileFilter      file_filter,
					       FileObjectifier file_objectifier)
			{
				this.path = path;
				this.file_filter = file_filter;
				this.file_objectifier = file_objectifier;
			}

			public IEnumerator GetEnumerator ()
			{
				FileEnumerator e;
				e = new FileEnumerator (path, file_filter, file_objectifier);
				e.NamesOnly = this.NamesOnly;
				return e;
			}
		}

		static private bool IsFile (string path, string name)
		{
			return File.Exists (Path.Combine (path, name));
		}

		static private object FileInfoObjectifier (string path, string name)
		{
			return new FileInfo (Path.Combine (path, name));
		}

		/////////////////////////////////////////////////////////////////////////////////

		static public bool IsWalkable (string dirPath)
		{
			DirInfo dir_handle;
            dirPath.Replace('/', '\\');
			dir_handle = opendir (dirPath);
			if (!dir_handle.lastResult)
				return false;
			closedir (dir_handle);
			return true;
		}

		/////////////////////////////////////////////////////////////////////////////////

		static public IEnumerable GetFiles (string path)
		{
			return new FileEnumerable (path, new FileFilter (IsFile), null);
		}

		static public IEnumerable GetFiles (DirectoryInfo dirinfo)
		{
			return GetFiles (dirinfo.FullName);
		}

		static public IEnumerable GetFileInfos (string path)
		{
			return new FileEnumerable (path,
						   new FileFilter (IsFile),
						   new FileObjectifier (FileInfoObjectifier));
		}

		static public IEnumerable GetFileInfos (DirectoryInfo dirinfo)
		{
			return GetFileInfos (dirinfo.FullName);
		}

		static private bool IsDirectory (string path, string name)
		{
			return Directory.Exists (Path.Combine (path, name));
		}

		static private object DirectoryInfoObjectifier (string path, string name)
		{
			return new DirectoryInfo (Path.Combine (path, name));
		}

		static public IEnumerable GetDirectories (string path)
		{
			return new FileEnumerable (path, new FileFilter (IsDirectory), null);
		}

		static public IEnumerable GetDirectories (DirectoryInfo dirinfo)
		{
			return GetDirectories (dirinfo.FullName);
		}

		static public IEnumerable GetDirectoryNames (string path)
		{
			FileEnumerable fe;
			fe = new FileEnumerable (path, new FileFilter (IsDirectory), null);
			fe.NamesOnly = true;
			return fe;
		}

		static public IEnumerable GetDirectoryInfos (string path)
		{
			return new FileEnumerable (path,
						   new FileFilter (IsDirectory),
						   new FileObjectifier (DirectoryInfoObjectifier));
		}

		static public IEnumerable GetDirectoryInfos (DirectoryInfo dirinfo)
		{
			return GetDirectoryInfos (dirinfo.FullName);
		}

		static public IEnumerable GetItems (string path, FileFilter filter)
		{
			return new FileEnumerable (path, filter, null);
		}

		static public IEnumerable GetItemNames (string path, FileFilter filter)
		{
			FileEnumerable fe;
			fe = new FileEnumerable (path, filter, null);
			fe.NamesOnly = true;
			return fe;
		}

        //returns full path
        static public IEnumerable GetItemsRecursive(string path, FileFilter filter)
        {
            foreach (string i in DirectoryWalker.GetItems(path, filter))
                yield return i;

            foreach (string dir in DirectoryWalker.GetDirectories(path))
            {
                if (IsWalkable(dir))
                {
                    foreach (string i in GetItemsRecursive(dir, filter))
                        yield return i;
                }
            }
            yield break;
        }

        //returns only filenames
		static public IEnumerable GetItemNamesRecursive(string path, FileFilter filter)
        {
            foreach (string i in DirectoryWalker.GetItemNames(path, filter))
                yield return i;

            foreach (string dir in DirectoryWalker.GetDirectories(path))
            {
                if (IsWalkable(dir))
                {
                    foreach (string i in GetItemNamesRecursive(dir, filter))
                        yield return i;
                }
            }
            yield break;
        }

		static public IEnumerable GetFileInfosRecursive (string path)
		{
			foreach (FileInfo i in DirectoryWalker.GetFileInfos (path))
				yield return i;

			foreach (string dir in DirectoryWalker.GetDirectories (path)) {
                if (IsWalkable(dir))
                {
                    foreach (FileInfo i in GetFileInfosRecursive(dir))
                        yield return i;
                }
			}

			yield break;
		}

		static public IEnumerable GetFileInfosRecursive (DirectoryInfo dirinfo)
		{
			return GetFileInfosRecursive (dirinfo.FullName);
		}

		static public int GetNumItems (string path)
		{
			int count = 0;
			FileFilter counting_filter = delegate (string dir, string name) {
							    count ++;
							    return false;
			};

			FileEnumerator dir_enum = new FileEnumerator (path, counting_filter, null);
			dir_enum.MoveNext ();
			return count;
		}

	}

}
