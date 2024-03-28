# Documents Index

This repo includes several documents that explain both high-level and low-level concepts about Ryujinx and its functions. These are very useful for contributors, to get context that can be very difficult to acquire from just reading code.

Intro to Ryujinx
==================

Ryujinx is an open-source Nintendo Switch emulator, created by gdkchan, written in C#. 
* The CPU emulator, ARMeilleure, emulates an ARMv8 CPU and currently has support for most 64-bit ARMv8 and some of the ARMv7 (and older) instructions.
* The GPU emulator emulates the Switch's Maxwell GPU using either the OpenGL (version 4.5 minimum), Vulkan, or Metal (via MoltenVK) APIs through a custom build of OpenTK or Silk.NET respectively.
* Audio output is entirely supported via C# wrappers for SDL2, with OpenAL & libsoundio as fallbacks.

Getting Started
===============

- [Installing the .NET SDK](https://dotnet.microsoft.com/download)
- [Official .NET Docs](https://docs.microsoft.com/dotnet/core/)

Contributing (Building, testing, benchmarking, profiling, etc.)
===============

If you want to contribute a code change to this repo, start here.

- [Contributor Guide](../CONTRIBUTING.md)

Coding Guidelines
=================

- [C# coding style](coding-guidelines/coding-style.md)
- [Service Implementation Guidelines - WIP](https://gist.github.com/gdkchan/84ba88cd50efbe58d1babfaa7cd7c455)

Project Docs
=================

To be added. Many project files will contain basic XML docs for key functions and classes in the meantime.
