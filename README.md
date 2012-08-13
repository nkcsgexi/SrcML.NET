﻿# SrcML.NET

This framework and associated tools are used within [ABB Corporate Research](http://www.abb.com/softwareresearch) to do both program transformation and code analysis.

It is based on the [srcML project](http://www.sdml.info/projects/srcml/) from [Kent State University Software Development Laboratory](http://www.sdml.info/index.html).

## Building

In order to build and run the library and tools you need to install [Visual Studio 2010](http://www.microsoft.com/visualstudio/en-us/products/2010-editions) and the [Visual Studio 2010 SP1 SDK](http://www.microsoft.com/en-us/download/details.aspx?id=21835).

You also need to set the environment variable `SRCMLBINDIR` to `[Solution Path]\External\bin\srcml`. This way, all of the tools will be able to find the srcML executables included with the code.

All of the build artifacts are stored in `[Solution Path]\Build` in either the `Debug` or `Release` subdirectories depending upon the selected configuration.

## Tools

Here are the tools included with the distribution along with brief instructions on how to run them.

### SrcML Converter (srcml.exe)

`srcml.exe` is a command line utility that facilitates both the creation of srcML archives from source code directories and the export of code directories from a srcML archive.

You can see help for these functions via the following commands:

For converting source directories to srcML (this is basically a convenience wrapper around KSU's src2srcml.exe tool):

    srcml.exe help src2srcml

For converting a srcML archive back to source code:

    srcml.exe help srcml2src

### Src2SrcMLPreview

`Src2SrcMLPreview.exe` is a helper application that lets you input source code snippets and then see how they are represented in srcML. Simply run the executable and paste your code into the first text box.

### Visual Studio Transform Preview Add-In

The preview add-in lets you create *transformation projects* that modify a srcML archive. In order to use it with Visual Studio, you first need to do the following:

1. Copy the file `[Solution Path]\VisualStudio\ABB.SrcML.VisualStudio.PreviewAddIn\SrcMLPreviewAddin - For Testing.AddIn` to `[Your Home Directory]\My Documents\Visual Studio 2010\Addins`.
2. Copy the file `[Solution Path]\VisualStudio\Templates\ABB SrcML Transform Project.zip` to `[Your Home Directory]\My Documents\Visual Studio 2010\Templates\ProjectTemplates`.
3. Copy the file `[Solution Path]\VisualStudio\Templates\ABB SrcML Transform Class.zip` to `[Your Home Directory]\My Documents\Visual Studio 2010\Templates\ItemTemplates`.

Once you've done this, you're ready to use the add-in.

1. Run Visual Studio 2010
2. Select `Tools → Add-in Manager...`
3. Check the box next to `SrcML Preview Pane (Debug)`
4. Press `OK`
5. Select `File → New → Project...`
6. Find the "ABB SrcML Transform Project" item under "Visual C#".
7. Name your project and press `OK`.

You now have a transformation project that you can use to query and transform C++ or Java projects. More documentation on using the add-in and writing transformations is coming.