; SPDX-License-Identifier: GPL-2.0-or-later
; Copyright (C) 2026 The iSpectrum contributors
;
; A first program for iSpectrum, assembled with sjasmplus:
;
;   sjasmplus --sym=hello.sym examples/hello.asm
;
; writes hello.tap (loadable in iSpectrum) and hello.sym (its symbols, which iSpectrum's
; debugger reads when it opens hello.tap) in the current folder.

        DEVICE ZXSPECTRUM48
        ORG $8000

CHAN_OPEN   EQU $1601           ; ROM: open a channel (A = 2: the upper screen)
PRINT_CHAR  EQU $10             ; ROM: RST $10 prints the character in A

start:
        ld a,2
        call CHAN_OPEN
        ld hl,message
.next:
        ld a,(hl)               ; the message ends with a 0
        or a
        jr z,.done
        rst PRINT_CHAR
        inc hl
        jr .next
.done:
        ret                     ; back to BASIC

message:
        db "Hello from sjasmplus!",13,0

        SAVETAP "hello.tap",start
