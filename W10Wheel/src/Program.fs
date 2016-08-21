﻿(*
 * Copyright (c) 2016 Yuki Ono
 * Licensed under the MIT License.
 *)

open System
open System.Diagnostics
open System.Runtime.InteropServices
open System.Threading
open System.Windows.Forms
open Microsoft.FSharp.NativeInterop
open Microsoft.Win32

let private messageDoubleLaunch () =
    MessageBox.Show("Double Launch?", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error) |> ignore

let private procExit () =
    Debug.WriteLine("procExit")

    WinHook.unhook()
    Ctx.storeProperties()
    PreventMultiInstance.unlock()

let private getBool (argv: string array) i =
    try
        if argv.Length = 1 then true else Boolean.Parse(argv.[1])
    with
        | :? FormatException as e ->
            Dialog.errorMessageE e
            Environment.Exit(0)
            false

let private setSelectedProperties name =
    if Properties.exists(name) then
        Ctx.setSelectedProperties name
    else
        Dialog.errorMessage (sprintf "'%s' properties does not exist." name) "Error"

let private procArgv (argv: string array) =
    Debug.WriteLine("procArgv")

    if argv.Length > 0 then
        match argv.[0] with
        | "--sendExit" -> W10Message.sendExit()
        | "--sendPassMode" -> W10Message.sendPassMode(getBool argv 1)
        | name -> setSelectedProperties name

        if argv.[0].StartsWith("--send") then
            Thread.Sleep(1000)
            Environment.Exit(0)

[<STAThread>]
[<EntryPoint>]
let main argv =
    procArgv argv

    if not (PreventMultiInstance.tryLock()) then
        messageDoubleLaunch()
        Environment.Exit(0)

    SystemEvents.SessionEnding.Add (fun _ -> procExit())

    WinHook.setMouseDispatcher(Dispatcher.getMouseDispatcher())
    WinHook.setKeyboardDispatcher(Dispatcher.getKeyboardDispatcher())
    EventHandler.setChangeTrigger()
    Windows.setInitScroll()

    Ctx.loadProperties()

    if Ctx.isDpiAware() then
        Windows.setProcessPerMonitorDpiAwareness()

    Ctx.setSystemTray()
    
    WinHook.setMouseHook()
    //Hook.setKeyboardHook()

    Application.Run()
    Debug.WriteLine("exit message loop")
    procExit()
    0
        
