CorrugatedIron Samples
======================

This repository contains a set of sample applications which demonstrate how to set up, configure and use [CorrugatedIron][] in both Visual Studio and Mono. The repository is broken up into two folders, one for Visual Studio samples and the other for Mono. Each set of samples is the same, the only difference is the environment in which they run.

The projects are:

* Sample.Unity - A simple project which demonstrates how to load up CorrugatedIron from the `app.config` file and wire together the components using Microsoft's [Unity][] IoC container. It also has a mini tutorial in code which attempts to explain how to use the API.
* Sample.YakRiak - This is a .NET client for Sean Cribb's [YakRiak][] application. It uses the configuration/setup from the `Sample.Unity` project and shows basic use of **Put** and **Async Streaming Map/Reduce** operations.

  [CorrugatedIron]: http://corrugatediron.org/ "CorrugatedIron - .NET Riak Client"
  [Unity]: http://unity.codeplex.com/ "Unity IoC"
  [YakRiak]: http://github.com/seancribbs/yakriak "YakRiak"
