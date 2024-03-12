@echo off
set xsd=W:\C#\Edifact\var\xsd
rem set input=W:\C#\Edifact\var\manifest.poll
set input=W:\C#\Edifact\var\booking.poll
set output=W:\C#\Edifact\var\out
W:\C#\Edifact\Tests\ParseAll\bin\Debug\net8.0\ParseAll.exe %xsd% %input% %output%