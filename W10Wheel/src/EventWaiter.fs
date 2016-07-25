﻿module EventWaiter

(*
 * Copyright (c) 2016 Yuki Ono
 * Licensed under the MIT License.
 *)

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Threading

open Mouse

let private waiting = ref false
let private sync = new BlockingCollection<MouseEvent>(1)

let offer e =
    sync.TryAdd(e)

//let add e = sync.Add(e)

let isWaiting () = Volatile.Read(waiting)

let private fromTimeout down =
    Ctx.LastFlags.SetResent down
    Debug.WriteLine(sprintf "wait Trigger (%s -->> Timeout): resend %s" down.Name down.Name)
    Windows.resendDown down

let private fromMove down =
    Ctx.LastFlags.SetResent down
    Debug.WriteLine(sprintf "wait Trigger (%s -->> Move): resend %s" down.Name down.Name)
    Windows.resendDown down

let private fromUp (down:MouseEvent) (up:MouseEvent) =
    Ctx.LastFlags.SetResent down

    let resendC (mc: MouseClick) =
        Debug.WriteLine(sprintf "wait Trigger (%s -->> %s): resend %s" down.Name up.Name mc.Name)
        Windows.resendClick mc

    let resendUD () =
        let wn = down.Name
        let rn = up.Name
        Debug.WriteLine(sprintf "wait Trigger (%s -->> %s): resend %s, %s" wn rn wn rn)
        Windows.resendDown down
        Windows.resendUp up

    match down with
    | LeftDown(_) ->
        match up with
        | LeftUp(_)  -> resendC(LeftClick(down.Info))
        | RightUp(_) -> resendUD()
        | _ -> raise (InvalidOperationException())
    | RightDown(_) ->
        match up with
        | RightUp(_) -> resendC(RightClick(down.Info))
        | LeftUp(_) -> resendUD()
        | _ -> raise (InvalidOperationException())
    | _ -> raise (InvalidOperationException())

let private fromDown (d1:MouseEvent) (d2:MouseEvent) =
    Ctx.LastFlags.SetSuppressed d1
    Ctx.LastFlags.SetSuppressed d2

    Debug.WriteLine(sprintf "wait Trigger (%s -->> %s): start scroll mode" d1.Name d2.Name)
    Ctx.startScrollMode d2.Info

let private waiterQueue = new BlockingCollection<MouseEvent>(1)

let private waiter () =
    let res: MouseEvent ref = ref NoneEvent
    while true do
        let down = waiterQueue.Take()
            
        Debug.WriteLine("EventWaiter: TryTake")
        let ts = new TimeSpan(0, 0, 0, 0, Ctx.getPollTimeout())
        let timeout = not (sync.TryTake(res, ts))
        Volatile.Write(waiting, false)

        if timeout then
            fromTimeout down
        else
            match res.Value with
            | Move(_) -> fromMove down
            | LeftUp(_) | RightUp(_) -> fromUp down res.Value
            | LeftDown(_) | RightDown(_) -> fromDown down res.Value
            | _ -> raise (InvalidOperationException())
        
let private waiterThread = new Thread(waiter)
waiterThread.IsBackground <- true
waiterThread.Start()

let start (down: MouseEvent) =
    if not (down.IsDown) then
        raise (ArgumentException())

    Volatile.Write(waiting, true)
    waiterQueue.Add(down)
