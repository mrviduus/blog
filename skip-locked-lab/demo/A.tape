# Records Terminal A. Run both tapes at the same time: see record.sh
Output A.mp4
Set Shell bash
Set FontSize 14
Set Width 780
Set Height 440
Set Padding 16
Set Framerate 15
Set TypingSpeed 30ms
Hide
Type "cd ../worker-cs && clear"
Enter
Show
Type "# Terminal A"
Enter

Type "dotnet run --project src/App -- A slow"
Enter
Sleep 32s
