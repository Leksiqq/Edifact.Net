@echo off
set xsd=W:\C#\Edifact\var\xsd
set what=manifest.poll
set input=W:\C#\Edifact\var\%what%
set output=W:\C#\Edifact\var\out\%what%
W:\C#\Edifact\Tests\ParseAll\bin\Debug\net8.0\ParseAll.exe %xsd% %input% %output%