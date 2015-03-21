The idea is to provide rapid and smart indexing of local documents, on windows.

When standard file **(O)**organisation can't help:

As i'm working in very large projects (50 developpers during 4-5 years, huge codebase), i need smart and quick access to information, contained in sources, technical documentation, etc.

This tool is targeted at offering the best answer with a few **(W)**words or keystrokes: where is the definition of class 'xxxx', where did i read something about derivating quaternions splines, where are my movies about augmented reality ?

It involves better logic while parsing files, and searching request, than available in the current offerings (live, google and copernic).

It's written in c#, using WPF. **(L)**Lucene.Net 2.4.0 is currently used as the backend. It's absolutely not targeted at protecting privacy, but indexing complete data drives (i'm using special code to enumerate ntfs drives).

I'm not sure about the licence and the code is very messy.
But yes, it's already usable :)
