using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSNDK.Parser;
using NUnit.Framework;

namespace GDBParser_UnitTests
{
    public class UnitTests
    {
        [Test]
        public void _TestGDB()
        {
            StringAssert.AreEqualIgnoringCase("", GDBParserUnitTests.GDBTests(), "GDB Error.");
        }
        [Test]
        public void CompareGDBFiles()
        {
            string obtainedFile = "", originalFile = "", errorsFile = "";
            System.IO.StreamReader obtainedFileStream = null;
            try
            {
                obtainedFileStream = new System.IO.StreamReader(Environment.GetEnvironmentVariable("APPDATA") + "\\BlackBerry\\parsedGDBResponse-output.log");
                obtainedFile = obtainedFileStream.ReadToEnd();
                obtainedFileStream.Close();
            }
            catch (Exception e)
            {
                obtainedFile = "";
            }

            if (obtainedFile == "")
            {
                errorsFile = "You have to run the test \"_TestGDB\" first, to generate the file \"parsedGDBResponse-output.log\".";
            }
            else
            {
                originalFile = obtainedFile;

                obtainedFile = obtainedFile.Replace("\"0x0\"", "NULL");

                while (obtainedFile.Contains("$\r\n$GDB - *running,thread-id="))
                {
                    int p1 = obtainedFile.IndexOf("$\r\n$GDB - *running,thread-id=\"all\"");
                    int p2 = obtainedFile.IndexOf("\r\n$\r\n", p1);
                    obtainedFile = obtainedFile.Substring(0, p1) + obtainedFile.Substring(p2 + 1);
                }

                int end, pos = obtainedFile.IndexOf("0x");
                while (pos != -1)
                {
                    string txt;
                    char limit = obtainedFile.ElementAt(pos - 1);
                    end = obtainedFile.IndexOf(limit, pos);

                    txt = obtainedFile.Substring(pos, end - pos);
                    obtainedFile = obtainedFile.Replace(txt, "_Address_");
                    pos = obtainedFile.IndexOf("0x", pos);
                }

                int counter = 1;

                pos = obtainedFile.IndexOf("[New pid");
                while (pos != -1)
                {
                    pos += 9;
                    end = obtainedFile.IndexOf(' ', pos);
                    if ((obtainedFile[pos] >= '0') && (obtainedFile[pos] <= '9'))
                    {
                        string txt = obtainedFile.Substring(pos, end - pos);
                        obtainedFile = obtainedFile.Replace(txt, "ProcessID_" + counter);
                        counter++;
                    }
                    pos = obtainedFile.IndexOf("[New pid", end);
                }

                pos = obtainedFile.IndexOf(",times=\"");
                while (pos != -1)
                {
                    pos += 8;
                    end = obtainedFile.IndexOf('\"', pos);

                    string txt = obtainedFile.Substring(pos, end - pos);
                    int i = -1;
                    try
                    {
                        i = Convert.ToInt32(txt);
                    }
                    catch
                    {
                        i = -1;
                    }
                    if (i != -1)
                    {
                        obtainedFile = obtainedFile.Substring(0, pos) + "xxx" + obtainedFile.Substring(end);

                        pos = obtainedFile.IndexOf("Parsed - 2", end);
                        end = obtainedFile.IndexOf('\n', pos);
                        pos = obtainedFile.LastIndexOf(';', end) + 1;

                        obtainedFile = obtainedFile.Substring(0, pos) + "xxx" + obtainedFile.Substring(end);
                    }
                    pos = obtainedFile.IndexOf(",times=\"", end);
                }

                obtainedFile = obtainedFile.Replace("\r", "");

                while (obtainedFile.IndexOf("\n\n") != -1)
                {
                    obtainedFile = obtainedFile.Replace("\n\n", "\n");
                }

                pos = obtainedFile.IndexOf("$GDB - ");
                while (pos != -1)
                {
                    end = obtainedFile.IndexOf("\n$", pos);
                    int newLineChar = obtainedFile.IndexOf('\n', pos);
                    while (newLineChar < end)
                    {
                        obtainedFile = obtainedFile.Substring(0, newLineChar) + " " + obtainedFile.Substring(newLineChar + 1);
                        newLineChar = obtainedFile.IndexOf('\n', pos);
                    }

                    pos = end + 2;
                    end = obtainedFile.IndexOf("\n$", pos);
                    newLineChar = obtainedFile.IndexOf('\n', pos);
                    while (newLineChar < end)
                    {
                        obtainedFile = obtainedFile.Substring(0, newLineChar) + " " + obtainedFile.Substring(newLineChar + 1);
                        newLineChar = obtainedFile.IndexOf('\n', pos);
                    }

                    pos = obtainedFile.IndexOf("$GDB - ", end);
                }

                obtainedFile = removeDoubleSpaces(obtainedFile);
                obtainedFile = obtainedFile.Replace("$ \n", "$\n");

                // Writing contents to file.
                System.IO.StreamWriter writeObtainedFile = null;
                try
                {
                    writeObtainedFile = new System.IO.StreamWriter(Environment.GetEnvironmentVariable("APPDATA") + "\\BlackBerry\\obtained-output.log");
                    writeObtainedFile.Write(obtainedFile);
                    writeObtainedFile.Close();
                }
                catch (Exception e)
                {
                }

                string expectedFile = "";
                System.IO.StreamReader expectedFileStream = null;
                try
                {
                    expectedFileStream = new System.IO.StreamReader(Environment.GetEnvironmentVariable("APPDATA") + "\\BlackBerry\\expected-output.log");
                    expectedFile = expectedFileStream.ReadToEnd();
                    expectedFileStream.Close();
                }
                catch (Exception e)
                {
                    expectedFile = "";
                }

                if (expectedFile == "")
                {
                    errorsFile = "You have to run the \"GenerateExpectedFile\" first, to generate the file \"expected-output.log\".";
                }
                else
                {
                    string expectedCommand, expectedGDBResponse, expectedParsedResponse,
                           obtainedCommand, obtainedGDBResponse, obtainedParsedResponse, obtainedLastCommand = "", obtainedLastGDBResponse = "",
                           logID = "";

                    int expPos, expEnd = 0;

                    end = 0;
                    pos = obtainedFile.IndexOf("$GDB - ");
                    expPos = expectedFile.IndexOf("$GDB - ");
                    while ((pos != -1) && (expPos != -1))
                    {
                        // obtain GDB command, response and parsed response from the obtained stream
                        pos = obtainedFile.LastIndexOf("\n$\n", pos) + 3;
                        if (obtainedFile.ElementAt(pos + 1) == 'C')
                        {
                            pos += 11; // 11 = size of "$Command - "
                            end = obtainedFile.IndexOf('\n', pos);
                            if (end == pos)
                                obtainedCommand = "";
                            else
                                obtainedCommand = obtainedFile.Substring(pos, end - pos);
                        }
                        else
                        {
                            obtainedCommand = "";
                            end = pos - 1;
                        }

                        pos = end + 8; // size of "\n$GDB - "
                        end = obtainedFile.IndexOf('\n', pos);
                        if (end > pos)
                            obtainedGDBResponse = obtainedFile.Substring(pos, end - pos);
                        else
                            obtainedGDBResponse = "";

                        pos = end + 11; // size of "\n$Parsed - "
                        end = obtainedFile.IndexOf('\n', pos);
                        if (end > pos)
                            obtainedParsedResponse = obtainedFile.Substring(pos, end - pos);
                        else
                            obtainedParsedResponse = "";

                        pos = end + 1; // size of "\n"
                        end = obtainedFile.IndexOf('\n', pos);
                        if (end > pos)
                            logID = obtainedFile.Substring(pos, end - pos);
                        else
                            logID = "";

                        // Finished obtaining GDB command, response and parsed response from the obtained stream

                        // obtain GDB command, response and parsed response from the expected stream. The expected file contents does not have double spaces.
                        expPos = expectedFile.LastIndexOf("\n$\n", expPos) + 3; // 3 = size of "/n$/n"; 11 = size of "$Command - "
                        if (expectedFile.ElementAt(expPos + 1) == 'C')
                        {
                            expPos += 11;
                            expEnd = expectedFile.IndexOf('\n', expPos);
                            if (expEnd == expPos)
                                expectedCommand = "";
                            else
                                expectedCommand = expectedFile.Substring(expPos, expEnd - expPos);
                        }
                        else
                        {
                            expectedCommand = "";
                            expEnd = expPos - 1;
                        }

                        expPos = expEnd + 8; // size of "\n$GDB - "
                        expEnd = expectedFile.IndexOf('\n', expPos);
                        if (expEnd > expPos)
                            expectedGDBResponse = expectedFile.Substring(expPos, expEnd - expPos);
                        else
                            expectedGDBResponse = "";

                        expPos = expEnd + 11; // size of "\n$Parsed - "
                        expEnd = expectedFile.IndexOf('\n', expPos);
                        if (expEnd > expPos)
                            expectedParsedResponse = expectedFile.Substring(expPos, expEnd - expPos);
                        else
                            expectedParsedResponse = "";
                        // Finished obtaining GDB command, response and parsed response from the expected stream

                        string originalMessage = "";
                        if (logID != "")
                        {
                            int aux_ini = originalFile.IndexOf(logID);
                            int aux_end = originalFile.IndexOf("\n", aux_ini);
                            aux_ini = originalFile.LastIndexOf("\r\n$\r\n", aux_end);
                            originalMessage = originalFile.Substring(aux_ini, aux_end - aux_ini);
                        }

                        if (expectedGDBResponse != obtainedGDBResponse)
                        {
                            if ((expectedGDBResponse.Contains("breakpoint-modified")) || (obtainedGDBResponse.Contains("breakpoint-modified")))
                            {
                                if ((expectedGDBResponse.Contains("breakpoint-modified")) && (obtainedGDBResponse.Contains("breakpoint-modified")))
                                {
                                    errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                                else
                                {
                                    if (obtainedLastGDBResponse.Contains("breakpoint-modified"))
                                    {
                                        if (obtainedGDBResponse.Contains("breakpoint-modified"))
                                        {
                                            int auxPos = pos;
                                            while (obtainedGDBResponse.Contains("breakpoint-modified"))
                                            {
                                                auxPos = obtainedFile.IndexOf("$GDB - ", auxPos);
                                                auxPos += 7; // size of "$GDB - "
                                                int auxEnd = obtainedFile.IndexOf('\n', auxPos);
                                                obtainedGDBResponse = obtainedFile.Substring(auxPos, auxEnd - auxPos);
                                            }
                                            pos = obtainedFile.LastIndexOf("$\n", auxPos);
                                            expPos = expectedFile.LastIndexOf("$\n", expPos);
                                        }
                                        else
                                        {
                                            int auxPos = expPos;
                                            while (expectedGDBResponse.Contains("breakpoint-modified"))
                                            {
                                                auxPos = expectedFile.IndexOf("$GDB - ", auxPos);
                                                auxPos += 7; // size of "$GDB - "
                                                int auxEnd = expectedFile.IndexOf('\n', auxPos);
                                                expectedGDBResponse = expectedFile.Substring(auxPos, auxEnd - auxPos);
                                            }
                                            expPos = expectedFile.LastIndexOf("$\n", auxPos);
                                            pos = obtainedFile.LastIndexOf("$\n", pos);
                                        }
                                    }
                                    else
                                    {
                                        errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                    }
                                }

                            }
                            else if ((obtainedLastCommand.Contains("-exec-interrupt")) && (!obtainedCommand.Contains("16kill")))
                            {
                                if (((expectedGDBResponse.Contains("*stopped,frame")) || (expectedGDBResponse.Contains("*stopped,reason=\"signal-received\"")))
                                    && ((obtainedGDBResponse.Contains("*stopped,frame")) || (obtainedGDBResponse.Contains("*stopped,reason=\"signal-received\""))))
                                {
                                    if (obtainedParsedResponse.Substring(0, 3) != "44;")
                                    {
                                        errorsFile += "\r\nProgram interruption was not parsed correctly: " + obtainedParsedResponse + ". \r\nExpected one                                 : " + expectedParsedResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                    }
                                }
                                else
                                {
                                    errorsFile += "\r\nWas expecting a program interruption: " + obtainedGDBResponse + ". \r\nExpected one                        : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                            }
                            else if (obtainedCommand.Contains("-stack-list-variables"))
                            {
                                string txt1 = removeBetweenQuotes(obtainedGDBResponse, ref obtainedParsedResponse, ",value=", 1);
                                string txt2 = removeBetweenQuotes(expectedGDBResponse, ref expectedParsedResponse, ",value=", 1);
                                if (txt1 != txt2)
                                {
                                    errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                                else if (obtainedParsedResponse != expectedParsedResponse)
                                {
                                    errorsFile += "\r\nResponse was not parsed correctly: " + obtainedParsedResponse + ". \r\nExpected one                     : " + expectedParsedResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                            }
                            else if (obtainedCommand.Contains("-thread-info"))
                            {
                                string txt1 = cleanThreadInfo(obtainedGDBResponse, ref obtainedParsedResponse);
                                string txt2 = cleanThreadInfo(expectedGDBResponse, ref expectedParsedResponse);
                                if (txt1 != txt2)
                                {
                                    errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                                else if (obtainedParsedResponse != expectedParsedResponse)
                                {
                                    errorsFile += "\r\nResponse was not parsed correctly: " + obtainedParsedResponse + ". \r\nExpected one                     : " + expectedParsedResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                            }
                            else if (obtainedCommand.Contains("-data-evaluate-expression"))
                            {
                                string txt1 = removeBetweenQuotes(obtainedGDBResponse, ref obtainedParsedResponse, ",value=", 2);
                                string txt2 = removeBetweenQuotes(expectedGDBResponse, ref expectedParsedResponse, ",value=", 2);
                                if (txt1 != txt2)
                                {
                                    errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                                else if (obtainedParsedResponse != expectedParsedResponse)
                                {
                                    errorsFile += "\r\nResponse was not parsed correctly: " + obtainedParsedResponse + ". \r\nExpected one                     : " + expectedParsedResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                            }
                            else if (obtainedGDBResponse.Contains("^running *running,thread-id="))
                            {
                                string txt1 = removeBetweenQuotes(obtainedGDBResponse, ref obtainedParsedResponse, ",thread-id=", 1);
                                string txt2 = removeBetweenQuotes(expectedGDBResponse, ref expectedParsedResponse, ",thread-id=", 1);
                                if (txt1 != txt2)
                                {
                                    errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                                else if (obtainedParsedResponse != expectedParsedResponse)
                                {
                                    if ((!obtainedParsedResponse.Contains("41;")) || (!expectedParsedResponse.Contains("41;")))
                                        errorsFile += "\r\nResponse was not parsed correctly: " + obtainedParsedResponse + ". \r\nExpected one                     : " + expectedParsedResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                }
                            }
                            else if ((obtainedCommand.Contains("16kill")) && (obtainedGDBResponse.Contains("kill")) && (expectedGDBResponse.Contains("kill")))
                            {
                                string gdbResponse = obtainedGDBResponse;
                                string expectedResponse = expectedGDBResponse;
                                if (obtainedGDBResponse.Length > expectedGDBResponse.Length)
                                {
                                    while (gdbResponse.Contains(expectedResponse))
                                    {
                                        gdbResponse = gdbResponse.Replace(expectedResponse, "");

                                        expPos = expectedFile.IndexOf("$GDB - ", expPos);

                                        if (expPos == -1)
                                            break;

                                        // obtain GDB response and parsed response from the expected stream. The expected file contents does not have double spaces.
                                        expPos = expectedFile.LastIndexOf("\n$\n", expPos) + 3; // 3 = size of "/n$/n"

                                        expPos = expPos + 7; // size of "$GDB - "
                                        expEnd = expectedFile.IndexOf('\n', expPos);
                                        if (expEnd > expPos)
                                            expectedResponse = expectedFile.Substring(expPos, expEnd - expPos);
                                        else
                                            expectedResponse = "";
                                        expectedGDBResponse += expectedResponse;

                                        expPos = expEnd + 11; // size of "\n$Parsed - "
                                        expEnd = expectedFile.IndexOf('\n', expPos);
                                        if (expEnd > expPos)
                                            expectedParsedResponse += expectedFile.Substring(expPos, expEnd - expPos);
                                        // Finished obtaining GDB command, response and parsed response from the expected stream
                                    }
                                    if (gdbResponse.Trim() != "")
                                    {
                                        errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                    }
                                }
                                else
                                {

                                    while (expectedResponse.Contains(gdbResponse))
                                    {
                                        expectedResponse = expectedResponse.Replace(gdbResponse, "");

                                        pos = obtainedFile.IndexOf("$GDB - ", pos);

                                        if (pos == -1)
                                            break;

                                        // obtain GDB response and parsed response from the obtained stream. The obtained file contents does not have double spaces.
                                        pos = obtainedFile.LastIndexOf("\n$\n", pos) + 3; // 3 = size of "/n$/n"

                                        pos = pos + 7; // size of "$GDB - "
                                        end = obtainedFile.IndexOf('\n', pos);
                                        if (end > pos)
                                            gdbResponse = obtainedFile.Substring(pos, end - pos);
                                        else
                                            gdbResponse = "";
                                        obtainedGDBResponse += gdbResponse;

                                        pos = end + 11; // size of "\n$Parsed - "
                                        end = obtainedFile.IndexOf('\n', pos);
                                        if (end > pos)
                                            obtainedParsedResponse += obtainedFile.Substring(pos, end - pos);
                                        // Finished obtaining GDB command, response and parsed response from the expected stream
                                    }
                                    if (expectedResponse.Trim() != "")
                                    {
                                        errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                                    }
                                }
                            }
                            else
                            {
                                errorsFile += "\r\nUnexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one           : " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                            }
                        }
                        else if (expectedParsedResponse != obtainedParsedResponse)
                        {
                            errorsFile += "\r\nResponse was not parsed correctly: " + obtainedParsedResponse + ". \r\nExpected one                     : " + expectedParsedResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
                        }
//                        else if (expectedCommand != obtainedCommand)
//                        {
//                            errorsFile += "Unexpected GDB response: " + obtainedGDBResponse + ". \r\nExpected one: " + expectedGDBResponse + ". \r\n In the original file: " + originalMessage + "\r\n\r\n";
//                        }

                        obtainedLastGDBResponse = obtainedGDBResponse;
                        if (obtainedCommand != "")
                            obtainedLastCommand = obtainedCommand;

                        if (pos != -1)
                            pos = obtainedFile.IndexOf("$GDB - ", pos);
                        if (expPos != -1)
                            expPos = expectedFile.IndexOf("$GDB - ", expPos);
                    }

                    if (pos != -1)
                    {
                        errorsFile += "These obtained commands/responses were not evaluated:\r\n" + obtainedFile.Substring(pos) + "\r\n\r\n";
                    }

                    if (expPos != -1)
                    {
                        errorsFile += "These expected commands/responses were not evaluated:\r\n" + expectedFile.Substring(expPos) + "\r\n\r\n";
                    }
                }
            }
            
            // Writing contents to file.
            System.IO.StreamWriter writeErrorsFile = null;
            try
            {
                writeErrorsFile = new System.IO.StreamWriter(Environment.GetEnvironmentVariable("APPDATA") + "\\BlackBerry\\errors-output.log");
                writeErrorsFile.Write(errorsFile);
                writeErrorsFile.Close();
            }
            catch (Exception e)
            {
            }

            StringAssert.AreEqualIgnoringCase(errorsFile, "", errorsFile);
        }

        private string cleanThreadInfo(string txt, ref string parsed)
        {
            int p1 = txt.IndexOf("name \\\"consumerThread\\\""), p2;
            if (p1 != -1)
            {
                p1 += 23; // size of "name \\\"consumerThread\\\""
                p2 = txt.IndexOf("},state", p1);
                txt = txt.Substring(0, p1) + txt.Substring(p2 + 1);

                p1 = parsed.IndexOf("name \\\"consumerThread\\\";") + 24;
                p2 = parsed.IndexOf(";consumerThread#", p1);
                parsed = parsed.Substring(0, p1) + parsed.Substring(p2);
            }

            p1 = txt.IndexOf("name \\\"producerThread\\\"");
            if (p1 != -1)
            {
                p1 += 23; // size of "name \\\"producerThread\\\""
                p2 = txt.IndexOf("},state", p1);
                txt = txt.Substring(0, p1) + txt.Substring(p2 + 1);

                p1 = parsed.IndexOf("name \\\"producerThread\\\";") + 24;
                p2 = parsed.IndexOf(";producerThread#", p1);
                parsed = parsed.Substring(0, p1) + parsed.Substring(p2);
            }
            return txt;
        }

        private string removeBetweenQuotes(string txt, ref string parsed, string value, int option)
        {
            int p1 = txt.IndexOf(value + "\""), p2;
            while (p1 != -1)
            {
                p1 += value.Length;
                if (option == 1)
                    p2 = txt.IndexOf("\"", p1 + 1) + 1;
                else
                    p2 = txt.LastIndexOf("\"") + 1;
                string temp = txt.Substring(p1, p2 - p1);
                txt = txt.Replace(temp, "");
                if (option == 1)
                    temp = ";" + temp.Replace("\"", "") + "#";
                parsed = parsed.Replace(temp, ";#");
                p1 = txt.IndexOf(value + "\"");
            }
            return txt;
        }

        private string removeDoubleSpaces(string txt)
        {
            while (txt.IndexOf("  ") != -1)
            {
                txt = txt.Replace("  ", " ");
            }
            return txt;
        }

        [Test]
        public void GetNextChar()
        {
            Assert.AreEqual(0, GDBParserUnitTests.get_Next_Char(Convert.ToSByte('a'), "abcdefghij", 0), "Error: char 'a', string 'abcdefghij', int '0'");
            Assert.AreEqual(10, GDBParserUnitTests.get_Next_Char(Convert.ToSByte('a'), "abcdefghij", 1), "Error: char 'a', string 'abcdefghij', int '1'");
            Assert.AreEqual(3, GDBParserUnitTests.get_Next_Char(Convert.ToSByte('d'), "abcdefghij", 0), "Error: char 'd', string 'abcdefghij', int '0'");
            Assert.AreEqual(3, GDBParserUnitTests.get_Next_Char(Convert.ToSByte('d'), "abcdefghij", 1), "Error: char 'd', string 'abcdefghij', int '1'");
            Assert.AreEqual(10, GDBParserUnitTests.get_Next_Char(Convert.ToSByte('z'), "abcdefghij", 1), "Error: char 'z', string 'abcdefghij', int '1'");
            Assert.AreEqual(0, GDBParserUnitTests.get_Next_Char(Convert.ToSByte('d'), "", 1), "Error: char 'd', string '', int '1'");
            Assert.AreEqual(10, GDBParserUnitTests.get_Next_Char(Convert.ToSByte('d'), "abcdefghij", 12), "Error: char 'd', string 'abcdefghij', int '12'");
            Assert.AreEqual(10, GDBParserUnitTests.get_Next_Char(Convert.ToSByte(null), "abcdefghij", 1), "Error: char NULL, string 'abcdefghij', int '1'");
        }

        [Test]
        public void findClosing()
        {
            Assert.AreEqual(1, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "()cdefghij", 0), "Error: char '(', char '), string '()cdefghij', int '0'");
            Assert.AreEqual(10, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "(bcdefghij", 2), "Error: char '(', char '), string '(bcdefghij', int '2'");
            Assert.AreEqual(10, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "(bcdefghij", 0), "Error: char '(', char '), string '(bcdefghij', int '0'");
            Assert.AreEqual(5, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "(()())ghij", 0), "Error: char '(', char '), string '(()())ghij', int '0'");
            Assert.AreEqual(7, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "((\\()())hij", 0), "Error: char '(', char '), string '((\\()())hij', int '0'");
            Assert.AreEqual(2, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "(()())ghij", 1), "Error: char '(', char '), string '(()())ghij', int '1'");
            Assert.AreEqual(4, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "((\\()())hij", 1), "Error: char '(', char '), string '((\\()())hij', int '1'");
            Assert.AreEqual(4, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "(()())ghij", 3), "Error: char '(', char '), string '(()())ghij', int '3'");
            Assert.AreEqual(6, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "(()(())hij", 3), "Error: char '(', char '), string '(()(())hij', int '3'");
            Assert.AreEqual(1, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "()())fghij", 0), "Error: char '(', char '), string '()())fghij', int '0'");
            Assert.AreEqual(3, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "()())fghij", 2), "Error: char '(', char '), string '()())fghij', int '2'");
            Assert.AreEqual(10, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "((())fghij", 0), "Error: char '(', char '), string '(()())ghij', int '0'");
            Assert.AreEqual(4, GDBParserUnitTests.find_Closing(Convert.ToSByte('('), Convert.ToSByte(')'), "((())fghij", 1), "Error: char '(', char '), string '((())fghij', int '1'");
        }

        [Test]
        public void searchResponse()
        {
            // Add the values you want to test.
            Assert.AreEqual(3, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 0, 1, true, Convert.ToSByte('?')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '0', int '1', bool 'TRUE', char '?'");
            Assert.AreEqual(1, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 0, 1, true, Convert.ToSByte('@')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '0', int '1', bool 'TRUE', char '@'");
            Assert.AreEqual(14, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 0, 3, true, Convert.ToSByte('?')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '0', int '3', bool 'TRUE', char '?'");
            Assert.AreEqual(12, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 0, 3, true, Convert.ToSByte('@')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '0', int '3', bool 'TRUE', char '@'");
            Assert.AreEqual(19, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 10, 3, true, Convert.ToSByte('?')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '10', int '3', bool 'TRUE', char '?'");
            Assert.AreEqual(17, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 10, 3, true, Convert.ToSByte('@')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '10', int '3', bool 'TRUE', char '@'");
            Assert.AreEqual(24, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 24, 1, false, Convert.ToSByte('?')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '24', int '1', bool 'TRUE', char '?'");
            Assert.AreEqual(24, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 24, 1, false, Convert.ToSByte('@')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '24', int '1', bool 'TRUE', char '@'");
            Assert.AreEqual(19, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 24, 3, false, Convert.ToSByte('?')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '24', int '3', bool 'TRUE', char '?'");
            Assert.AreEqual(19, GDBParserUnitTests.search_Response("aabbbcaabaccabcababaababc", "ab", 24, 3, false, Convert.ToSByte('@')), "Error: string 'aabbbcaabaccabcababaababc', string 'ab', int '24', int '3', bool 'TRUE', char '@'");
        }
        
        [Test]
        public void substituteVariables()
        {
            string[] v = new string[10];
            v[0] = "The";
            v[1] = "text";
            v[2] = "";
            v[3] = "";
            v[4] = "";
            v[5] = "the";
            v[6] = "";
            v[7] = "";
            v[8] = "";
            v[9] = "number";
            // Add the values you want to test.
            StringAssert.AreEqualIgnoringCase("The number of the words in the following text is smaller than the number of the letters in the same text\r\n", GDBParserUnitTests.substitute_Variables("$0$ $9$ of $5$ words in $5$ following $1$ is smaller $3$than $5$ $9$ of $5$ letters in $5$ same $1$$EOL$", v), "Error: string '', Variables array not printed.");
        }

        [Test]
        public void insertingCommandCodes()
        {
            string[,] map = new string[48, 2]; // The value for the first column of this array corresponds to the value of NumberOfInstructions constant, in gdb-connect.h.
            string[] v = new string[48]; // The size of this array corresponds to the value of NumberOfInstructions constant, in gdb-connect.h.
            bool result = GDBParserUnitTests.inserting_Command_Codes(map, v);
            Assert.AreEqual(true, result);
            Assert.IsNotEmpty(map, "Hash table could not be empty!");
            Assert.IsNotEmpty(v, "Parsing instructions array could not be empty!");
        }

        [Test]
        public void getInstructionCode()
        {
            // The instruction code returned from get_Instruction_Code() function depends on the position the associated parsing instruction
            // was stored in the respective data structure. That's why the following unit tests just compares the instruction code with -1
            // (-1 means that the instruction was not found).
            string param = "";
            // Add the values you want to test.
            Assert.AreEqual(-1, GDBParserUnitTests.get_Instruction_Code("", out param), "Error: string '', string ''");
            StringAssert.AreEqualIgnoringCase("", param, "Param should be empty.");

            param = "";
            Assert.AreNotEqual(-1, GDBParserUnitTests.get_Instruction_Code("-exec-continue --thread-group i1", out param), "Error: string '-exec-continue --thread-group i1', string ''");
            StringAssert.AreEqualIgnoringCase("", param, "Param should be empty for command '-exec-continue --thread-group i1'.");

            param = "";
            Assert.AreNotEqual(-1, GDBParserUnitTests.get_Instruction_Code("-break-delete 3", out param), "Error: string '-break-delete 3', string ''");
            StringAssert.AreEqualIgnoringCase(";3", param, "Param should not be empty for command '-break-delete 3'.");

            param = "";
            Assert.AreNotEqual(-1, GDBParserUnitTests.get_Instruction_Code("-break-after 4 10", out param), "Error: string '-break-after 4 10', string ''");
            StringAssert.AreEqualIgnoringCase(";4;10", param, "Param should not be empty for command '-break-after 4 10'.");
        }

        [Test]
        public void get_SeqID()
        {
            // Add the values you want to test.
            Assert.AreEqual(211, GDBParserUnitTests.get_Seq_ID("211^done,value=\"1\""), "Error: string '211^done,value=\"1\"'");
            Assert.AreEqual(111, GDBParserUnitTests.get_Seq_ID("111^running"), "Error: string '111^running'");
            Assert.AreEqual(-3, GDBParserUnitTests.get_Seq_ID("=breakpoint-modified,bkpt={number=\"1\",type=\"breakpoint\",disp=\"keep\",enabled=\"y\",addr=\"0xf7fd8502\",func=\"pendfunc1\",file=\"gdb/testsuite/gdb.mi/pendshr1.c\",fullname=\"/unsafegdb/testsuite/gdb.mi/pendshr1.c\",line=\"21\",times=\"0\",original-location=\"pendfunc1\"}"), "Error: string '=breakpoint-modified,bkpt={number=\"1\",type=\"breakpoint\",disp=\"keep\",enabled=\"y\",addr=\"0xf7fd8502\",func=\"pendfunc1\",file=\"gdb/testsuite/gdb.mi/pendshr1.c\",fullname=\"/unsafegdb/testsuite/gdb.mi/pendshr1.c\",line=\"21\",times=\"0\",original-location=\"pendfunc1\"}'");
            Assert.AreEqual(-2, GDBParserUnitTests.get_Seq_ID("*stopped,reason=\"breakpoint-hit\",disp=\"keep\",bkptno=\"2\",frame={func=\"foo\",args=[],file=\"hello.c\",fullname=\"/home/foo/bar/hello.c\",line=\"13\"}"), "Error: string '*stopped,reason=\"breakpoint-hit\",disp=\"keep\",bkptno=\"2\",frame={func=\"foo\",args=[],file=\"hello.c\",fullname=\"/home/foo/bar/hello.c\",line=\"13\"}'");
            Assert.AreEqual(-2, GDBParserUnitTests.get_Seq_ID("111*stopped,signal-name=\"SIGINT\",signal-meaning=\"Interrupt\",frame={addr=\"0x00010140\",func=\"foo\",args=[],file=\"try.c\",line=\"13\"}"), "Error: string '111*stopped,signal-name=\"SIGINT\",signal-meaning=\"Interrupt\",frame={addr=\"0x00010140\",func=\"foo\",args=[],file=\"try.c\",line=\"13\"}'");
            Assert.AreEqual(-1, GDBParserUnitTests.get_Seq_ID("abcde"), "Error: string 'abcde'");
            Assert.AreEqual(-1, GDBParserUnitTests.get_Seq_ID(""), "Error: string ''");
        }

        [Test]
        public void parseGDB1()
        {
            string asyncInst = "??=thread-exited,id;{#51\\;;??\";@@\";#$EOL$;};??thread-created,id;{#40\\;;??\";@@\";#\\;;??pid ;@@ ;#$EOL$;};0;??*running;{#41\\;;??thread-id=\"all;{#0;}{??thread-id=\";@@\";};#$EOL$;};0;??*stopped;{??breakpoint-hit;{#27\\;;??bkptno=\";@@\";#\\;;??file=\";@@\";#\\;;??line=\";@@\";}{??end-stepping-range;{#45;??file=\";{#\\;;@@\";0;#\\;;??line=\";@@\";};}{??function-finished;{#46;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};}{??exited-normally;{#42;}{??exit-code;{#43\\;;??\";@@\";}{??signal-received;{??signal-meaning=\"Killed\";{#47;}{??signal-meaning=\"Segmentation fault\";{#54\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};}{#44\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};};};}{??exited-signalled;{#55\\;??signal-name=\";@@\";#\\;;??signal-meaning=\";@@\";}{#44\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};};};};};};};};#\\;;??thread-id=;{??\";@@\";};#$EOL$;};0;??=breakpoint-modified,bkpt=\\{number=\";{0;($EOR$;$1=?<?=breakpoint-modified,bkpt=\\{number=\";@@\";$$;$EOR$;?<?=breakpoint-modified,bkpt=\\{number=\"$1$;#21\\;$1$\\;;??enabled=\";@@\";#\\;;??addr=\";@@\";#\\;;??func=\";@@\";#\\;;??file=\";@@\";#\\;;??line=\";@@\";#\\;;??times=\";@@\";0;(??=breakpoint-modified,bkpt=\\{number=\"$1$;%?<?$EOL$;@@\"\\};);):$EOL$;};0;??=breakpoint-deleted,id=;{0;(??=breakpoint-deleted,id=\";#22\\;;@@\";):$EOL$;};0;??@\";{0;#81\\;\";(??@\";?<?@;@@$EOL$;):$EOL$;#\"!81$EOL$;};0;(??~\";#80\\;\";?<?~;@@$EOL$;#\"!80;):$EOL$;??Error in testing breakpoint condition;{#29\\;$EOL$;};??Quit (expect signal SIGINT when the program is resumed);{#50\\;$EOL$;};??2374: internal-error: frame_cleanup_after_sniffer: Assertion;{#52\\;$EOL$;};??^error,msg=\"Remote communication error: No error.;{#53\\;$EOL$;};??: internal-error: handle_inferior_event: Assertion;{#56\\;$EOL$;};0;(??&\";#80\\;\";?<?&;@@$EOL$;#\"!80;):$EOL$;(??$EOL$=;#80\\;\";?<?=;@@$EOL$;#\"!80;):$EOL$;";
            // Add the values you want to test.
            StringAssert.AreEqualIgnoringCase("23;1", GDBParserUnitTests.parse_GDB("12^done;1\r\n", "??^done;{#23;@@$EOL$};"), "Error: string '12^done;', string '??^done;{#23;@@$EOL$};'");
            StringAssert.AreEqualIgnoringCase("26;1;100", GDBParserUnitTests.parse_GDB("4^done;1;100\r\n", "??^done;{#26;@@$EOL$};"), "Error: string '4^done;1;100', string '??^done;{#26;@@$EOL$};'");
            StringAssert.AreEqualIgnoringCase("20;1;y;0x08048564;main;myprog.c;68;0", GDBParserUnitTests.parse_GDB("73^done,bkpt={number=\"1\",type=\"breakpoint\",disp=\"keep\",enabled=\"y\",addr=\"0x08048564\",func=\"main\",file=\"myprog.c\",fullname=\"/home/nickrob/myprog.c\",line=\"68\",thread-groups=[\"i1\"],times=\"0\"}", "??^done;{#20\\;;??number=\";{@@\";};#\\;;??enabled=\";{@@\";};#\\;;??addr=\";{@@\";};#\\;;??func=\";{@@\";};#\\;;??file=\";{@@\";};#\\;;??line=\";{@@\";};#\\;;??times=\";{@@\";};}{#Function not found!;};"), "Error: string '-break-insert', string ''");
            StringAssert.AreEqualIgnoringCase("27;1;myprog.c;68;\r\n", GDBParserUnitTests.parse_GDB("*stopped,reason=\"breakpoint-hit\",disp=\"keep\",bkptno=\"1\",thread-id=\"0\",frame={addr=\"0x08048564\",func=\"main\",args=[{name=\"argc\",value=\"1\"},{name=\"argv\",value=\"0xbfc4d4d4\"}],file=\"myprog.c\",fullname=\"/home/nickrob/myprog.c\",line=\"68\"}", asyncInst), "Error: string 'breakpoint hit.', string 'Asynchronous instruction.'");
            StringAssert.AreEqualIgnoringCase("42;\r\n", GDBParserUnitTests.parse_GDB("*stopped,reason=\"exited-normally\"", asyncInst), "Error: string '*stopped,reason=\"exited-normally\"', string 'AsyncInstruction'");
            StringAssert.AreEqualIgnoringCase("45;../../../devo/gdb/testsuite/gdb.threads/linux-dp.c;187;\r\n", GDBParserUnitTests.parse_GDB("*stopped,reason=\"end-stepping-range\",thread-id=\"2\",line=\"187\",file=\"../../../devo/gdb/testsuite/gdb.threads/linux-dp.c\"", asyncInst), "Error: string 'end-stepping-range.', string 'AsyncInstruction'");
            StringAssert.AreEqualIgnoringCase("46;hello.c;7;\r\n", GDBParserUnitTests.parse_GDB("*stopped,reason=\"function-finished\",frame={func=\"main\",args=[],file=\"hello.c\",fullname=\"/home/foo/bar/hello.c\",line=\"7\"}", asyncInst), "Error: string 'Function finished.', string 'AsyncInstruction'");
            StringAssert.AreEqualIgnoringCase("44;0x00010140;foo;try.c;13;\r\n", GDBParserUnitTests.parse_GDB("111*stopped,signal-name=\"SIGINT\",signal-meaning=\"Interrupt\",frame={addr=\"0x00010140\",func=\"foo\",args=[],file=\"try.c\",fullname=\"/home/foo/bar/try.c\",line=\"13\"}", asyncInst), "Error: string '-exec-interrupt', string 'AsyncInstruction'");
            StringAssert.AreEqualIgnoringCase("", GDBParserUnitTests.parse_GDB("", ""), "Error: string '', string ''");
        }

        [Test]
        public void parseGDB2()
        {
            string[] v = new string[10];
            v[0] = "";
            v[1] = "";
            v[2] = "";
            v[3] = "";
            v[4] = "";
            v[5] = "";
            v[6] = "";
            v[7] = "";
            v[8] = "";
            v[9] = "";
            string sep = "#"; // default separator.

            string asyncInst = "??=thread-exited,id;{#51\\;;??\";@@\";#$EOL$;};??thread-created,id;{#40\\;;??\";@@\";#\\;;??pid ;@@ ;#$EOL$;};0;??*running;{#41\\;;??thread-id=\"all;{#0;}{??thread-id=\";@@\";};#$EOL$;};0;??*stopped;{??breakpoint-hit;{#27\\;;??bkptno=\";@@\";#\\;;??file=\";@@\";#\\;;??line=\";@@\";}{??end-stepping-range;{#45;??file=\";{#\\;;@@\";0;#\\;;??line=\";@@\";};}{??function-finished;{#46;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};}{??exited-normally;{#42;}{??exit-code;{#43\\;;??\";@@\";}{??signal-received;{??signal-meaning=\"Killed\";{#47;}{??signal-meaning=\"Segmentation fault\";{#54\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};}{#44\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};};};}{??exited-signalled;{#55\\;??signal-name=\";@@\";#\\;;??signal-meaning=\";@@\";}{#44\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};};};};};};};};#\\;;??thread-id=;{??\";@@\";};#$EOL$;};0;??=breakpoint-modified,bkpt=\\{number=\";{0;($EOR$;$1=?<?=breakpoint-modified,bkpt=\\{number=\";@@\";$$;$EOR$;?<?=breakpoint-modified,bkpt=\\{number=\"$1$;#21\\;$1$\\;;??enabled=\";@@\";#\\;;??addr=\";@@\";#\\;;??func=\";@@\";#\\;;??file=\";@@\";#\\;;??line=\";@@\";#\\;;??times=\";@@\";0;(??=breakpoint-modified,bkpt=\\{number=\"$1$;%?<?$EOL$;@@\"\\};);):$EOL$;};0;??=breakpoint-deleted,id=;{0;(??=breakpoint-deleted,id=\";#22\\;;@@\";):$EOL$;};0;??@\";{0;#81\\;\";(??@\";?<?@;@@$EOL$;):$EOL$;#\"!81$EOL$;};0;(??~\";#80\\;\";?<?~;@@$EOL$;#\"!80;):$EOL$;??Error in testing breakpoint condition;{#29\\;$EOL$;};??Quit (expect signal SIGINT when the program is resumed);{#50\\;$EOL$;};??2374: internal-error: frame_cleanup_after_sniffer: Assertion;{#52\\;$EOL$;};??^error,msg=\"Remote communication error: No error.;{#53\\;$EOL$;};??: internal-error: handle_inferior_event: Assertion;{#56\\;$EOL$;};0;(??&\";#80\\;\";?<?&;@@$EOL$;#\"!80;):$EOL$;(??$EOL$=;#80\\;\";?<?=;@@$EOL$;#\"!80;):$EOL$;";
            // Add the values you want to test.
            StringAssert.AreEqualIgnoringCase("23;1", GDBParserUnitTests.parse_GDB("12^done;1\r\n", "??^done;{#23;@@$EOL$};", 0, false, v, sep), "Error: string '12^done;', string '??^done;{#23;@@$EOL$};'");
            StringAssert.AreEqualIgnoringCase("26;1;100", GDBParserUnitTests.parse_GDB("4^done;1;100\r\n", "??^done;{#26;@@$EOL$};", 0, false, v, sep), "Error: string '4^done;1;100', string '??^done;{#26;@@$EOL$};'");
            StringAssert.AreEqualIgnoringCase("20;1;y;0x08048564;main;myprog.c;68;0", GDBParserUnitTests.parse_GDB("73^done,bkpt={number=\"1\",type=\"breakpoint\",disp=\"keep\",enabled=\"y\",addr=\"0x08048564\",func=\"main\",file=\"myprog.c\",fullname=\"/home/nickrob/myprog.c\",line=\"68\",thread-groups=[\"i1\"],times=\"0\"}", "??^done;{#20\\;;??number=\";{@@\";};#\\;;??enabled=\";{@@\";};#\\;;??addr=\";{@@\";};#\\;;??func=\";{@@\";};#\\;;??file=\";{@@\";};#\\;;??line=\";{@@\";};#\\;;??times=\";{@@\";};}{#Function not found!;};", 0, false, v, sep), "Error: string '-break-insert', string ''");
            StringAssert.AreEqualIgnoringCase("27;1;myprog.c;68;\r\n", GDBParserUnitTests.parse_GDB("*stopped,reason=\"breakpoint-hit\",disp=\"keep\",bkptno=\"1\",thread-id=\"0\",frame={addr=\"0x08048564\",func=\"main\",args=[{name=\"argc\",value=\"1\"},{name=\"argv\",value=\"0xbfc4d4d4\"}],file=\"myprog.c\",fullname=\"/home/nickrob/myprog.c\",line=\"68\"}", asyncInst, 0, false, v, sep), "Error: string 'breakpoint hit.', string 'Asynchronous instruction.'");
            StringAssert.AreEqualIgnoringCase("42;\r\n", GDBParserUnitTests.parse_GDB("*stopped,reason=\"exited-normally\"", asyncInst, 0, false, v, sep), "Error: string '*stopped,reason=\"exited-normally\"', string 'AsyncInstruction'");
            StringAssert.AreEqualIgnoringCase("45;../../../devo/gdb/testsuite/gdb.threads/linux-dp.c;187;\r\n", GDBParserUnitTests.parse_GDB("*stopped,reason=\"end-stepping-range\",thread-id=\"2\",line=\"187\",file=\"../../../devo/gdb/testsuite/gdb.threads/linux-dp.c\"", asyncInst, 0, false, v, sep), "Error: string 'end-stepping-range.', string 'AsyncInstruction'");
            StringAssert.AreEqualIgnoringCase("46;hello.c;7;\r\n", GDBParserUnitTests.parse_GDB("*stopped,reason=\"function-finished\",frame={func=\"main\",args=[],file=\"hello.c\",fullname=\"/home/foo/bar/hello.c\",line=\"7\"}", asyncInst, 0, false, v, sep), "Error: string 'Function finished.', string 'AsyncInstruction'");
            StringAssert.AreEqualIgnoringCase("44;0x00010140;foo;try.c;13;\r\n", GDBParserUnitTests.parse_GDB("111*stopped,signal-name=\"SIGINT\",signal-meaning=\"Interrupt\",frame={addr=\"0x00010140\",func=\"foo\",args=[],file=\"try.c\",fullname=\"/home/foo/bar/try.c\",line=\"13\"}", asyncInst, 0, false, v, sep), "Error: string '-exec-interrupt', string 'AsyncInstruction'");
            StringAssert.AreEqualIgnoringCase("", GDBParserUnitTests.parse_GDB("", "", 0, false, v, sep), "Error: string '', string ''");
        }

        [Test]
        public void testingInputBuffer()
        {
            GDBParserUnitTests.clean_Buffers();

            Assert.AreEqual(true, GDBParser.is_Input_Buffer_Empty(), "Input buffer should be empty.");

            for (int i = 0; i < 49; i++)
                Assert.AreEqual(true, GDBParserUnitTests.add_Into_Input_Buffer("cmd_" + i), "Could not add cmd_" + i + " in Input Buffer.");

            Assert.AreEqual(false, GDBParser.is_Input_Buffer_Empty(), "Input buffer should not be empty.");

            // to be sure that the Input buffer is circular, it is being deleted 20 elements and then adding them again.

            for (int i = 0; i < 20; i++)
            {
                string input = GDBParserUnitTests.remove_From_Input_Buffer();
                StringAssert.AreNotEqualIgnoringCase("", input, "" + i);
            }

            for (int i = 0; i < 20; i++)
                Assert.AreEqual(true, GDBParserUnitTests.add_Into_Input_Buffer("cmd_" + i), "Could not add cmd_" + i + " in Input Buffer.");

            for (int i = 0; i < 49; i++)
            {
                string input = GDBParserUnitTests.remove_From_Input_Buffer();
                StringAssert.AreNotEqualIgnoringCase("", input, "" + i);
            }

            Assert.AreEqual(true, GDBParser.is_Input_Buffer_Empty(), "Input buffer should be empty.");

            Assert.AreEqual(true, GDBParserUnitTests.add_Into_Input_Buffer("lastCmd"), "Could not add lastCmd in Input Buffer.");

            Assert.AreEqual(false, GDBParser.is_Input_Buffer_Empty(), "Input buffer should not be empty.");

            GDBParserUnitTests.clean_Buffers();

            Assert.AreEqual(true, GDBParser.is_Input_Buffer_Empty(), "Input buffer should be empty.");
        }

        [Test]
        public void testingGDBBuffer()
        {
            GDBParserUnitTests.clean_Buffers();

            Assert.AreEqual(false, GDBParserUnitTests.add_Into_GDB_Buffer(-1, -1, "resp_-1"), "It added resp_-1 in position -1 of Output Buffer! That should not be possible!");
            Assert.AreEqual(false, GDBParserUnitTests.add_Into_GDB_Buffer(-6, -6, "resp_-6"), "It added resp_-6 in position -6 of Output Buffer! That should not be possible!");

            Assert.AreEqual(true, GDBParserUnitTests.add_Into_GDB_Buffer(173, 173, "resp_" + 173), "Could not add resp_" + 173 + " in position " + 73 + " of Output Buffer.");
            Assert.AreEqual(true, GDBParserUnitTests.add_Into_GDB_Buffer(2314, 2314, "resp_" + 2314), "Could not add resp_" + 2314 + " in position " + 14 + " of Output Buffer.");
            Assert.AreEqual(false, GDBParserUnitTests.add_Into_GDB_Buffer(714, 714, "resp_" + 714), "Could not add resp_" + 714 + " in position " + 14 + " of Output Buffer.");

            string param = "";
            Assert.AreEqual(-7, GDBParserUnitTests.remove_From_GDB_Buffer(-7, out param));
            StringAssert.AreEqualIgnoringCase("", param, "-7");
            param = "";
            Assert.AreEqual(173, GDBParserUnitTests.remove_From_GDB_Buffer(73, out param));
            StringAssert.AreEqualIgnoringCase("resp_173", param, "73");
            param = "";
            Assert.AreEqual(2314, GDBParserUnitTests.remove_From_GDB_Buffer(14, out param));
            StringAssert.AreEqualIgnoringCase("resp_2314", param, "14");

            GDBParserUnitTests.clean_Buffers();

            Assert.AreEqual(true, GDBParserUnitTests.is_GDB_Buffer_Empty(), "GDB buffer should be empty. (1)");

            for (int i = 0; i < 100; i++)
                Assert.AreEqual(true, GDBParserUnitTests.add_Into_GDB_Buffer(i, i, "param_" + i), "Could not add seq_id=" + i + "; instruction code=" + i + "; param=param_" + i + " in GDB Buffer.");

            Assert.AreEqual(false, GDBParserUnitTests.add_Into_GDB_Buffer(16, 16, "param_" + 16), "Could not add seq_id=" + 16 + "; instruction code=" + 16 + "; param=param_" + 16 + " in GDB Buffer.");
            
            Assert.AreEqual(false, GDBParserUnitTests.is_GDB_Buffer_Empty(), "GDB buffer should not be empty.");

            for (int i = 0; i < 100; i++)
            {
                param = "";
                Assert.AreEqual(i, GDBParserUnitTests.remove_From_GDB_Buffer(i, out param));
                StringAssert.AreEqualIgnoringCase("param_" + i, param, "" + i);
            }

            Assert.AreEqual(true, GDBParserUnitTests.is_GDB_Buffer_Empty(), "GDB buffer should be empty. (2)");

            Assert.AreEqual(true, GDBParserUnitTests.add_Into_GDB_Buffer(21, 21, "param_21"), "Could not add lastCmd in GDB Buffer.");

            Assert.AreEqual(false, GDBParserUnitTests.is_GDB_Buffer_Empty(), "GDB buffer should not be empty. (3)");

            GDBParserUnitTests.clean_Buffers();

            Assert.AreEqual(true, GDBParserUnitTests.is_GDB_Buffer_Empty(), "GDB buffer should be empty. (4)");
        }

        [Test]
        public void testingOutputBuffer()
        {
            GDBParserUnitTests.clean_Buffers();

            Assert.AreEqual(false, GDBParserUnitTests.add_Into_Output_Buffer(-1, "resp_-1"), "It added resp_-1 in position -1 of Output Buffer! That should not be possible!");
            Assert.AreEqual(false, GDBParserUnitTests.add_Into_Output_Buffer(-6, "resp_-6"), "It added resp_-6 in position -6 of Output Buffer! That should not be possible!");

            Assert.AreEqual(true, GDBParserUnitTests.add_Into_Output_Buffer(173, "resp_" + 173), "Could not add resp_" + 173 + " in position " + 73 + " of Output Buffer.");
            Assert.AreEqual(true, GDBParserUnitTests.add_Into_Output_Buffer(2314, "resp_" + 2314), "Could not add resp_" + 2314 + " in position " + 14 + " of Output Buffer.");
            Assert.AreEqual(false, GDBParserUnitTests.add_Into_Output_Buffer(714, "resp_" + 714), "Could not add resp_" + 714 + " in position " + 14 + " of Output Buffer.");

            string output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(-3);
            StringAssert.AreEqualIgnoringCase("", output, "-3");
            output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(73);
            StringAssert.AreEqualIgnoringCase("", output, "73");
            output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(14);
            StringAssert.AreNotEqualIgnoringCase("", output, "14");

            GDBParserUnitTests.clean_Buffers();

            for (int i = 0; i < 100; i++)
                Assert.AreEqual(true, GDBParserUnitTests.add_Into_Output_Buffer(i, "resp_" + i), "Could not add resp_" + i + " in position " + i + " of Output Buffer.");
                        
            // testing the circular part of Output buffer, by deleting 20 elements and then adding them again.
            for (int i = 0; i < 20; i++)
            {
                output = GDBParserUnitTests.remove_From_Output_Buffer();
                StringAssert.AreNotEqualIgnoringCase("", output, "" + i);
            }

            // The circular part of Output buffer starts in position 50.
            for (int i = 50; i < 70; i++)
                Assert.AreEqual(true, GDBParserUnitTests.add_Into_Output_Buffer(i, "resp_" + i), "Could not add resp_" + i + " in position " + i + " of Output Buffer.");

            for (int i = 0; i < 50; i++)
            {
                output = GDBParserUnitTests.remove_From_Output_Buffer();
                StringAssert.AreNotEqualIgnoringCase("", output, "" + i);
            }

            // testing the first 50 positions of Output buffer, the "Synchronous" part. Deleting 20 elements and adding them again.
            for (int i = 0; i < 20; i++)
            {
                output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(i);
                StringAssert.AreNotEqualIgnoringCase("", output, "" + i);
            }

            // The circular part of Output buffer starts in position 50.
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(true, GDBParserUnitTests.add_Into_Output_Buffer(i, "resp_" + i), "Could not add resp_" + i + " in position " + i + " of Output Buffer.");

            for (int i = 0; i < 50; i++)
            {
                output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(i);
                StringAssert.AreNotEqualIgnoringCase("", output, "" + i);
            }
            
            string asyncInst = "??=thread-exited,id;{#51\\;;??\";@@\";#$EOL$;};??thread-created,id;{#40\\;;??\";@@\";#\\;;??pid ;@@ ;#$EOL$;};0;??*running;{#41\\;;??thread-id=\"all;{#0;}{??thread-id=\";@@\";};#$EOL$;};0;??*stopped;{??breakpoint-hit;{#27\\;;??bkptno=\";@@\";#\\;;??file=\";@@\";#\\;;??line=\";@@\";}{??end-stepping-range;{#45;??file=\";{#\\;;@@\";0;#\\;;??line=\";@@\";};}{??function-finished;{#46;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};}{??exited-normally;{#42;}{??exit-code;{#43\\;;??\";@@\";}{??signal-received;{??signal-meaning=\"Killed\";{#47;}{??signal-meaning=\"Segmentation fault\";{#54\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};}{#44\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};};};}{??exited-signalled;{#55\\;??signal-name=\";@@\";#\\;;??signal-meaning=\";@@\";}{#44\\;;??addr=\";@@\";#\\;;??func=\";@@\";0;??file=;{#\\;;??\";@@\";#\\;;??line=\";@@\";};};};};};};};};#\\;;??thread-id=;{??\";@@\";};#$EOL$;};0;??=breakpoint-modified,bkpt=\\{number=\";{0;($EOR$;$1=?<?=breakpoint-modified,bkpt=\\{number=\";@@\";$$;$EOR$;?<?=breakpoint-modified,bkpt=\\{number=\"$1$;#21\\;$1$\\;;??enabled=\";@@\";#\\;;??addr=\";@@\";#\\;;??func=\";@@\";#\\;;??file=\";@@\";#\\;;??line=\";@@\";#\\;;??times=\";@@\";0;(??=breakpoint-modified,bkpt=\\{number=\"$1$;%?<?$EOL$;@@\"\\};);):$EOL$;};0;??=breakpoint-deleted,id=;{0;(??=breakpoint-deleted,id=\";#22\\;;@@\";):$EOL$;};0;??@\";{0;#81\\;\";(??@\";?<?@;@@$EOL$;):$EOL$;#\"!81$EOL$;};0;(??~\";#80\\;\";?<?~;@@$EOL$;#\"!80;):$EOL$;??Error in testing breakpoint condition;{#29\\;$EOL$;};??Quit (expect signal SIGINT when the program is resumed);{#50\\;$EOL$;};??2374: internal-error: frame_cleanup_after_sniffer: Assertion;{#52\\;$EOL$;};??^error,msg=\"Remote communication error: No error.;{#53\\;$EOL$;};??: internal-error: handle_inferior_event: Assertion;{#56\\;$EOL$;};0;(??&\";#80\\;\";?<?&;@@$EOL$;#\"!80;):$EOL$;(??$EOL$=;#80\\;\";?<?=;@@$EOL$;#\"!80;):$EOL$;";
            // Add the values you want to test.
            GDBParserUnitTests.parse_GDB("12^done;1\r\n", "??^done;{#23;@@$EOL$};", 5);
            output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(5);
            StringAssert.AreEqualIgnoringCase("23;1", output, "23;1" + 5);
            output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(5);
            StringAssert.AreEqualIgnoringCase("", output, "" + 5);

            GDBParserUnitTests.parse_GDB("4^done;1;100\r\n", "??^done;{#26;@@$EOL$};", 45);
            output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(45);
            StringAssert.AreEqualIgnoringCase("26;1;100", output, "26;1;100" + 45);

            // to reset the out_OutputBuffer pointer, that points to the next position from the circular part of Output Buffer to be taken.
            GDBParserUnitTests.clean_Buffers();

            // Parsing some samples of GDB responses and adding the results in the Output Buffer.
            GDBParserUnitTests.parse_GDB("73^done,bkpt={number=\"1\",type=\"breakpoint\",disp=\"keep\",enabled=\"y\",addr=\"0x08048564\",func=\"main\",file=\"myprog.c\",fullname=\"/home/nickrob/myprog.c\",line=\"68\",thread-groups=[\"i1\"],times=\"0\"}", "??^done;{#20\\;;??number=\";{@@\";};#\\;;??enabled=\";{@@\";};#\\;;??addr=\";{@@\";};#\\;;??func=\";{@@\";};#\\;;??file=\";{@@\";};#\\;;??line=\";{@@\";};#\\;;??times=\";{@@\";};}{#Function not found!;};", 51);
            GDBParserUnitTests.parse_GDB("*stopped,reason=\"breakpoint-hit\",disp=\"keep\",bkptno=\"1\",thread-id=\"0\",frame={addr=\"0x08048564\",func=\"main\",args=[{name=\"argc\",value=\"1\"},{name=\"argv\",value=\"0xbfc4d4d4\"}],file=\"myprog.c\",fullname=\"/home/nickrob/myprog.c\",line=\"68\"}", asyncInst, 53);
            GDBParserUnitTests.parse_GDB("*stopped,reason=\"exited-normally\"", asyncInst, 52);
            GDBParserUnitTests.parse_GDB("*stopped,reason=\"end-stepping-range\",thread-id=\"2\",line=\"187\",file=\"../../../devo/gdb/testsuite/gdb.threads/linux-dp.c\"", asyncInst, 36);
            GDBParserUnitTests.parse_GDB("*stopped,reason=\"function-finished\",frame={func=\"main\",args=[],file=\"hello.c\",fullname=\"/home/foo/bar/hello.c\",line=\"7\"}", asyncInst, 50);
            GDBParserUnitTests.parse_GDB("111*stopped,signal-name=\"SIGINT\",signal-meaning=\"Interrupt\",frame={addr=\"0x00010140\",func=\"foo\",args=[],file=\"try.c\",fullname=\"/home/foo/bar/try.c\",line=\"13\"}", asyncInst, 23);

            // Getting the parsed responses from the synchronous part of Output buffer
            output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(23);
            StringAssert.AreEqualIgnoringCase("44;0x00010140;foo;try.c;13;\r\n", output, "44;0x00010140;foo;try.c;13;\r\n" + 23);

            output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(36);
            StringAssert.AreEqualIgnoringCase("45;../../../devo/gdb/testsuite/gdb.threads/linux-dp.c;187;\r\n", output, "45;../../../devo/gdb/testsuite/gdb.threads/linux-dp.c;187;\r\n" + 86);

            // Getting the parsed responses from the circular part of Output Buffer
            for (int i = 50; i < 100; i++)
            {
                output = GDBParserUnitTests.remove_From_Output_Buffer();
                switch (i)
                {
                    case 50:
                        StringAssert.AreEqualIgnoringCase("46;hello.c;7;\r\n", output, "46;hello.c;7;\r\n" + 50);
                        break;
                    case 51:
                        StringAssert.AreEqualIgnoringCase("20;1;y;0x08048564;main;myprog.c;68;0", output, "20;1;y;0x08048564;main;myprog.c;68;0" + 51);
                        break;
                    case 52:
                        StringAssert.AreEqualIgnoringCase("42;\r\n", output, "42;\r\n" + 52);
                        break;
                    case 53:
                        StringAssert.AreEqualIgnoringCase("27;1;myprog.c;68;\r\n", output, "27;1;myprog.c;68;\r\n" + 53);
                        break;
                    default:
                        StringAssert.AreEqualIgnoringCase("", output, "" + i);
                        break;
                }
            }

            GDBParserUnitTests.parse_GDB("", "", 5);
            output = GDBParserUnitTests.remove_Sync_From_Output_Buffer(5);
            // parse_GDB changes an empty parsed response from GDB to "$#@EMPTY@#$", so the method that is waiting for this parsed response
            // knows that the response was really parsed and returned an empty string.
            StringAssert.AreEqualIgnoringCase("$#@EMPTY@#$", output, "$#@EMPTY@#$" + 5);
        }

    }

    public class GenerateExpectedFile
    {
        [Test]
        public void generateExpectedFile()
        {
            string obtainedFile = "", errorsFile = "";
            System.IO.StreamReader obtainedFileStream = null;
            try
            {
                obtainedFileStream = new System.IO.StreamReader(Environment.GetEnvironmentVariable("APPDATA") + "\\BlackBerry\\parsedGDBResponse-output.log");
                obtainedFile = obtainedFileStream.ReadToEnd();
                obtainedFileStream.Close();
            }
            catch (Exception e)
            {
                obtainedFile = "";
            }

            if (obtainedFile == "")
            {
                errorsFile = "You have to run the test \"_TestGDB\" first, to generate the file \"parsedGDBResponse-output.log\".";
            }
            else
            {
                obtainedFile = obtainedFile.Replace("\"0x0\"", "NULL");

                while (obtainedFile.Contains("$\r\n$GDB - *running,thread-id="))
                {
                    int p1 = obtainedFile.IndexOf("$\r\n$GDB - *running,thread-id=\"all\"");
                    int p2 = obtainedFile.IndexOf("\r\n$\r\n", p1);
                    obtainedFile = obtainedFile.Substring(0, p1) + obtainedFile.Substring(p2 + 1);
                }

                int end, pos = obtainedFile.IndexOf("0x");
                while (pos != -1)
                {
                    string txt;
                    char limit = obtainedFile.ElementAt(pos - 1);
                    end = obtainedFile.IndexOf(limit, pos);

                    txt = obtainedFile.Substring(pos, end - pos);
                    obtainedFile = obtainedFile.Replace(txt, "_Address_");
                    pos = obtainedFile.IndexOf("0x", pos);
                }

                int counter = 1;

                pos = obtainedFile.IndexOf("[New pid");
                while (pos != -1)
                {
                    pos += 9;
                    end = obtainedFile.IndexOf(' ', pos);
                    if ((obtainedFile[pos] >= '0') && (obtainedFile[pos] <= '9'))
                    {
                        string txt = obtainedFile.Substring(pos, end - pos);
                        obtainedFile = obtainedFile.Replace(txt, "ProcessID_" + counter);
                        counter++;
                    }
                    pos = obtainedFile.IndexOf("[New pid", end);
                }

                pos = obtainedFile.IndexOf(",times=\"");
                while (pos != -1)
                {
                    pos += 8;
                    end = obtainedFile.IndexOf('\"', pos);

                    string txt = obtainedFile.Substring(pos, end - pos);
                    int i = -1;
                    try
                    {
                        i = Convert.ToInt32(txt);
                    }
                    catch
                    {
                        i = -1;
                    }
                    if (i != -1)
                    {
                        obtainedFile = obtainedFile.Substring(0, pos) + "xxx" + obtainedFile.Substring(end);

                        pos = obtainedFile.IndexOf("Parsed - 2", end);
                        end = obtainedFile.IndexOf('\n', pos);
                        pos = obtainedFile.LastIndexOf(';', end) + 1;

                        obtainedFile = obtainedFile.Substring(0, pos) + "xxx" + obtainedFile.Substring(end);
                    }
                    pos = obtainedFile.IndexOf(",times=\"", end);
                }

                obtainedFile = obtainedFile.Replace("\r", "");

                while (obtainedFile.IndexOf("\n\n") != -1)
                {
                    obtainedFile = obtainedFile.Replace("\n\n", "\n");
                }

                pos = obtainedFile.IndexOf("$GDB - ");
                while (pos != -1)
                {
                    end = obtainedFile.IndexOf("\n$", pos);
                    int newLineChar = obtainedFile.IndexOf('\n', pos);
                    while (newLineChar < end)
                    {
                        obtainedFile = obtainedFile.Substring(0, newLineChar) + " " + obtainedFile.Substring(newLineChar + 1);
                        newLineChar = obtainedFile.IndexOf('\n', pos);
                    }

                    pos = end + 2;
                    end = obtainedFile.IndexOf("\n$", pos);
                    newLineChar = obtainedFile.IndexOf('\n', pos);
                    while (newLineChar < end)
                    {
                        obtainedFile = obtainedFile.Substring(0, newLineChar) + " " + obtainedFile.Substring(newLineChar + 1);
                        newLineChar = obtainedFile.IndexOf('\n', pos);
                    }

                    pos = obtainedFile.IndexOf("$GDB - ", end);
                }

                while (obtainedFile.IndexOf("  ") != -1)
                {
                    obtainedFile = obtainedFile.Replace("  ", " ");
                }

                obtainedFile = obtainedFile.Replace("$ \n", "$\n");

                // Writing contents to file.
                System.IO.StreamWriter writeObtainedFile = null;
                try
                {
                    writeObtainedFile = new System.IO.StreamWriter(Environment.GetEnvironmentVariable("APPDATA") + "\\BlackBerry\\expected-output.log");
                    writeObtainedFile.Write(obtainedFile);
                    writeObtainedFile.Close();
                }
                catch (Exception e)
                {
                }
            }

            StringAssert.AreEqualIgnoringCase(errorsFile, "", errorsFile);
        }
    }

}
