# TinyBASICBlazor

A [Tiny BASIC](https://en.wikipedia.org/wiki/Tiny_BASIC) environment in your web browser.

## About the project

![TinyBASICBlazor screenshot](images\screenshot.jpg)

TinyBASICBlazor is a [Blazor WebAssembly](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) [Tiny BASIC](https://en.wikipedia.org/wiki/Tiny_BASIC) environment, that allows to type and run Tiny BASIC programs in your web browser. TinyBASICBlazor is based on [TinyBasic.NET](http://www.thisiscool.com/tinybasicnet.htm), [Mohan Embar](http://www.thisiscool.com/)'s [C#](http://www.thisiscool.com/tinybasicnet.htm) [port]() of [Tom Pittman](http://www.ittybittycomputers.com/)'s [Tiny Basic](http://www.ittybittycomputers.com/IttyBitty/TinyBasic/index.htm) interpreter [C rewrite](http://www.ittybittycomputers.com/IttyBitty/TinyBasic/TinyBasic.c). Some sample BASIC programs are included.

[Try it online](https://retrobits.altervista.org/tinybasicblazor/): https://retrobits.altervista.org/tinybasicblazor/

## Getting started

First, clone the repo:

```shell
git clone https://github.com/marcosretrobits/TinyBASICBlazor.git
```

or [download it as a .zip archive](https://github.com/marcosretrobits/TinyBASICBlazor/archive/refs/heads/master.zip).

### Microsoft Visual Studio

If you have Microsoft Visual Studio (currently tested with Visual Studio 2022), open the TinyBasicBlazor.sln solution file and start debugging with `F5`.

### .NET CLI

If you don't have Visual Studio, you can build and run the project using the .NET command-line interface (CLI). The .NET CLI is included in the .NET SDK; for more information about how to install the .NET SDK, see [Microsoft documentation about installing .NET Core](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

* Open a command prompt and change directory to the folder in which the repo has been cloned/unzipped;

* Build the solution:

  ```shell
  dotnet build TinyBasicBlazor.sln
  ```

* Run the TinyBasicBlazor project:

  ```shell
  dotnet run --project TinyBasicBlazor
  ```

* Open the https://localhost:5001 or http://localhost:5000 URL in your web browser.

#### Publishing TinyBASICBlazor

* Publish the application to a folder (e.g. "pub"):

  ```sh
  dotnet publish TinyBasicBlazor.sln -o pub
  ```

* Upload the "wwwroot" subfolder to your hosting system.
