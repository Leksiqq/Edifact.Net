@echo off
set xsd=F:\leksi\C#\Edifact\var\xsd
set input=F:\leksi\C#\Edifact\var\manifest.poll
set output=F:\leksi\C#\Edifact\var\out
F:\leksi\C#\Edifact\Tests\ParseAll\bin\Debug\net8.0\ParseAll.exe %xsd% %input% %output%