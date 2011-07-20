CorrugatedIron Samples
======================

This repository contains a set of sample applications which demonstrate how to set up, configure and use [CorrugatedIron][] in both Visual Studio and Mono. The repository is broken up into two folders, one for Visual Studio samples and the other for Mono. Each set of samples is the same, the only difference is the environment in which they run.

The projects are:

* Sample - A simple project which demonstrates how to load up CorrugatedIron from the `app.config` file and wire together the components using many of the popular IoC containers. The code includes how to wire up CorrugatedIron using:
    * [Autofac][]
    * [Ninject][]
    * [StructureMap][]
    * [TinyIoC][]
    * [Unity][]
* Sample.YakRiak - This is a .NET client for Sean Cribb's [YakRiak][] application. It uses the configuration/setup from the `Sample` project (using [Unity][]) and shows basic use of **Put** and **Async Streaming Map/Reduce** operations.

  [Autofac]: http://code.google.com/p/autofac/ "Autofac IoC"
  [CorrugatedIron]: http://corrugatediron.org/ "CorrugatedIron - .NET Riak Client"
  [Ninject]: http://ninject.org/ "Ninject IoC"
  [StructureMap]: http://structuremap.net/structuremap/ "StructureMap IoC"
  [TinyIoC]: https://github.com/grumpydev/TinyIoC "TinyIoC"
  [Unity]: http://unity.codeplex.com/ "Unity IoC"
  [YakRiak]: http://github.com/seancribbs/yakriak "YakRiak"
