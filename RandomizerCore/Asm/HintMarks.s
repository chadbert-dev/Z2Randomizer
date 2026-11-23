.include "z2r.inc"

; Archipelago: a white "?" over the head of each town NPC whose text is a helpful hint. It stays after the player
; reads the hint. Assembled only in Archipelago mode with helpful hints on (Hyrule.AddHintMarks).
;
; A helpful hint is a text slot of ARCHIPELAGO_HINT_SIZE bytes that ends in an $FF end byte and $80 + the hint
; number (CustomTexts.ArchipelagoHintPlaceholder). The Archipelago client rewrites the text but keeps that shape.
;
; Drawing: the game draws each object through bank7_Display ($ef11), which jumps to the object's own routine at
; $ef42 with Y = the object's OAM offset. Each object owns 6 sprites there and town NPCs draw 4, so the mark goes
; in the 5th, after the object's routine has run (it hides the sprites it doesn't use). The NPC text tables and the texts are in bank 3, which is mapped while a town runs.

HINT_SIZE = 52
HINT_MARK_TILE = $ff            ; 8x16 sprite from the fixed $1C00 page: tile $fe over the "?" in $ff (ArchipelagoHintMark.cs)
HINT_MARK_PALETTE = 1           ; sprite palette: the glyph is in color 3, white in towns
HINT_MARK_OAM = $10             ; the 5th sprite of the object's OAM block
HINT_MARK_DX = 4                ; the "?" is in the middle of the 8 pixel tile, so this centers it over a 16 pixel NPC
HINT_MARK_DY = 18               ; the "?" is in the bottom 8 rows of the sprite, so it sits 3 pixels above the NPC and the blank top tile stays clear of the NPC
MAX_HINTS = 8                   ; bits in HintsReadMask

FIRST_NPC_TYPE = $0a
LAST_NPC_TYPE = $22
RANDOM_TEXT_FIRST = $19         ; Z2R gives these NPC types a random text, so they never hold a hint
RANDOM_TEXT_LAST = $20
STATE1_ROWS = 16                ; the dialog state 1 tables have fewer rows than the state 0 tables
SIDE_VIEW_MODE = $0b

; bank 3
TextPointerTables = $b421       ; a word for each WorldNumber: that world's text pointer table
NpcTextWestState0 = $a238       ; text number for each NPC type (row) and town (TownNumber & 3)
NpcTextWestState1 = $a29c
NpcTextEastState0 = $a2dc
NpcTextEastState1 = $a340
DialogStartWithPointerSet = $b614

ObjectDisplayDispatch = $ef42   ; ldy $91,x / jmp ($000e)
ObjectOamOffset = $91
ScreenY = $00                   ; set by bank7_Display for the object being drawn
ScreenX = $01
TextPointer = $0569
TalkCount = $05a5               ; per object slot, read by the $0a/$0b dialog handler

.segment "PRG7"

.org ObjectDisplayDispatch
    jmp HintMarkDisplay

.reloc
HintMarkDisplay:
    ; Outside towns there is nothing to draw: do what vanilla does and tail-jump to the object's routine. Only Y is
    ; touched (A passes through), and the routine's RTS returns to the caller.
    ldy WorldNumber
    cpy #1
    beq @Town
    cpy #2
    beq @Town
    ldy ObjectOamOffset,x
    jmp ($000e)
@Town:
    ; Draw the object first: its routine hides the sprites it doesn't use, which would hide the mark
    ldy ObjectOamOffset,x
    jsr CallObjectDisplay
    php
    pha
    txa
    pha
    tya
    pha
    ldx EnemyIndex
    cpx #6
    bcs @Exit
    lda GameMode
    cmp #SIDE_VIEW_MODE
    bne @Exit
    lda $0c
    pha
    lda $0d
    pha
    lda $0e
    pha
    lda $0f
    pha
    jsr DrawHintMark
    pla
    sta $0f
    pla
    sta $0e
    pla
    sta $0d
    pla
    sta $0c
@Exit:
    pla
    tay
    pla
    tax
    pla
    plp
    rts

CallObjectDisplay:
    jmp ($000e)

; X = object slot (also in EnemyIndex). Uses $0c-$0f.
DrawHintMark:
    lda Enemy0Status,x
    cmp #1
    bne @Done
    lda EnemyType,x
    sec
    sbc #FIRST_NPC_TYPE
    bcc @Done
    cmp #LAST_NPC_TYPE - FIRST_NPC_TYPE + 1
    bcs @Done
    cmp #RANDOM_TEXT_FIRST - FIRST_NPC_TYPE
    bcc @Known
    cmp #RANDOM_TEXT_LAST - FIRST_NPC_TYPE + 1
    bcc @Done
@Known:
    lda EnemyType,x
    jsr DialogState
    bcs @Done                   ; this NPC's text does not come from the town tables
    sta $0d                     ; dialog state the game will pick
    ldx EnemyIndex
    lda EnemyType,x
    sec
    sbc #FIRST_NPC_TYPE
    asl a
    asl a
    sta $0c                     ; row * 4: each row has a text for 4 towns
    lda $0d
    beq @RowOk
    lda $0c
    cmp #STATE1_ROWS * 4
    bcs @Done
@RowOk:
    ; ($0e) = the text table for this world and state
    lda WorldNumber
    and #2                      ; 0 west, 2 east
    ora $0d
    asl a
    tay
    lda NpcTextTables,y
    sta $0e
    lda NpcTextTables+1,y
    sta $0f
    lda TownNumber
    and #3
    ora $0c
    tay
    lda ($0e),y
    jsr IsHintText
    bcs @Draw
@Done:
    rts
@Draw:
    ; The NPC is already drawn: put the mark above the top-left of its first 4 sprites
    ldx EnemyIndex              ; IsHintText used X; the draw routines keep the slot here
    lda ObjectOamOffset,x
    tax
    lda #$ff
    sta $0c                     ; top
    sta $0d                     ; left
    ldy #4
@Scan:
    lda SpriteYPosition,x
    cmp #$ef
    bcs @Next                   ; hidden
    cmp $0c
    bcs @Left
    sta $0c
@Left:
    lda SpriteXPosition,x
    cmp $0d
    bcs @Next
    sta $0d
@Next:
    inx
    inx
    inx
    inx
    dey
    bne @Scan
    txa
    sec
    sbc #4 * 4
    tax
    lda $0c
    cmp #$ef
    bcs @Done                   ; nothing drawn
    sbc #HINT_MARK_DY - 1       ; carry is clear here
    bcc @Done
    tay
    lda $0d
    clc
    adc #HINT_MARK_DX
    bcs @Done
    sta SpriteXPosition + HINT_MARK_OAM,x
    tya
    sta SpriteYPosition + HINT_MARK_OAM,x
    lda #HINT_MARK_TILE
    sta SpriteTilenumber + HINT_MARK_OAM,x
    lda #HINT_MARK_PALETTE
    sta SpriteAttributes + HINT_MARK_OAM,x
    rts

NpcTextTables:
    .word NpcTextWestState0, NpcTextWestState1, NpcTextEastState0, NpcTextEastState1

; A = NPC type, X = object slot. Picks the dialog state the way the game will when the player talks: the handler
; table at bank 3 $B44E, run from $B47F. Only the flag reads are copied, not the flag writes.
; C = 1 if the NPC's text does not come from the town text tables (wizards and others that jump to $8D40, $8D53 or
; $8FA4). Else C = 0 and A = the dialog state, 0 or 1. Uses Y, $0e, $0f. Keeps X.
DialogState:
    ldy TownNumber              ; the handlers compare $056b as it is
    cmp #$0c
    bcs @Not0ab
    ; $0a/$0b ($b5b9): state 1 from the 4th talk on
    lda TalkCount,x
    cmp #4
    lda #0
    rol a
    clc
    rts
@Not0ab:
    beq @State0                 ; $0c ($b5c7)
    cmp #$0d
    bne @Not0d
    ; $0d ($b4a3): town 0 state 0, town 2 state 1 once $079a bit 3 is set, towns 1 and 3 not from the tables
    cpy #0
    beq @State0
    cpy #2
    bne @Special
    lda $079a
    and #$08
    bne @State1
    beq @State0
@Not0d:
    cmp #$0e
    bne @Not0e
    ; $0e ($b4ee): town 1 state 1 once $0796 bit 1 is set, town 2 state 1 once $078a is set, else state 0
    cpy #1
    bne @Not0eTown1
    lda $0796
    and #$02
    bne @State1
    beq @State0
@Not0eTown1:
    cpy #2
    bne @State0
    lda $078a
    bne @State1
    beq @State0
@Not0e:
    cmp #$0f
    beq @Special                ; $0f ($b518) is not from the tables
    cmp #$13
    bcc @State0                 ; $10-$12 ($b54e)
    cmp #$17
    bcs @Not13
    ; $13-$16 ($b560): town 0 state 1, else state 1 once ($0797,town & $b42e[(type - $13) * 8 + town]) is set
    cpy #0
    beq @State1
    sec
    sbc #$13
    asl a
    asl a
    asl a
    sta $0e
    tya
    clc
    adc $0e
    stx $0f
    tax
    lda $0797,y
    and $b42e,x                 ; bank 3
    ldx $0f
    cmp #0
    bne @State1
    beq @State0
@Not13:
    cmp #$19
    bcs @Not17
    ; $17/$18 ($b57d): state 1 unless $074c is 2
    lda $074c
    cmp #2
    bne @State1
    beq @State0
@Not17:
    cmp #$21
    beq @Special                ; $21 ($b5ae) is not from the tables
    ; $19-$20 and $22: state 0
@State0:
    lda #0
    clc
    rts
@State1:
    lda #1
    clc
    rts
@Special:
    sec
    rts

; A = text number in the current world's table. C = 1 if it is a helpful hint, read or not.
; Uses $0e/$0f.
IsHintText:
    asl a
    tay
    lda WorldNumber
    asl a
    tax
    lda TextPointerTables,x
    sta $0e
    lda TextPointerTables+1,x
    sta $0f
    lda ($0e),y
    pha
    iny
    lda ($0e),y
    sta $0f
    pla
    sta $0e
    jmp HintNumber              ; C and A as HintNumber returns them; the read mask is not checked

; ($0e) = a text. C = 1 and A = the hint number if the text is a helpful hint. Uses Y.
HintNumber:
    ldy #HINT_SIZE - 2
    lda ($0e),y
    cmp #$ff
    bne @No
    iny
    lda ($0e),y
    cmp #$80
    bcc @No
    cmp #$a0
    bcs @No
    and #$1f
    sec
    rts
@No:
    clc
    rts

HintBits:
    .byte $01, $02, $04, $08, $10, $20, $40, $80

; The "Register your name" screen drew its two cursors with tile $ff, which now holds the "?". Draw them from the
; boss bar tile $c5 instead, flipped vertically. Sprite palette 2 makes it white ($82 = palette 2 + vertical flip).
.segment "PRG5"

.org $b75a                      ; name cursor: lda #$ff / sta $0205
    .byte $c5
.org $b75f                      ; lda #$03 / sta $0206
    .byte $82
.org $b86d                      ; letter grid cursor: lda #$ff / sta $0245
    .byte $c5
.org $b872                      ; lda #$03 / sta $0246
    .byte $82

; When a dialog starts, remember a helpful hint as read
.segment "PRG3"

.org DialogStartWithPointerSet
    jsr MarkHintRead
    nop
    nop

.segment "PRG7"

.reloc
MarkHintRead:
    txa
    pha
    tya
    pha
    lda $0e
    pha
    lda $0f
    pha
    lda TextPointer
    sta $0e
    lda TextPointer+1
    sta $0f
    jsr HintNumber
    bcc @Exit
    cmp #MAX_HINTS
    bcs @Exit
    tax
    ldy CurrentGameSlot
    cpy #3
    bcs @Exit
    lda HintBits,x
    ora HintsReadMask,y         ; one mask per save file
    sta HintsReadMask,y
@Exit:
    pla
    sta $0f
    pla
    sta $0e
    pla
    tay
    pla
    tax
    ; what the hook replaced (vanilla $b614: lda #$2a / sta $0566). ROM.cs "Make text go fast" patches the operand
    ; at $b615 to $00 before this runs, so replay $00: the vanilla $2a would bring back the slow text delay.
    lda #$00
    sta $0566
    rts
