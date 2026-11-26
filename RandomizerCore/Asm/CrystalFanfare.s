.include "z2r.inc"

; Archipelago mode. After a crystal is placed, the statue plays fanfare $40 with Link frozen, then waits for $07fb
; (the music now playing) to reach 0 before refilling and unfreezing him. The client can request another track in
; the meantime (the item-get jingle, $10), after which the looping area music resumes and $07fb never reaches 0.
; So wait only while the crystal fanfare itself is playing.

.segment "PRG4"

.org $9b47 ; ldy $07fb
    jsr CrystalFanfarePlaying

.reloc
CrystalFanfarePlaying:
    lda $07fb
    and #$40
    tay ; Y = 0 and Z set once the fanfare is over; the dey that follows the bne then gives the $ff refill amount
    rts
