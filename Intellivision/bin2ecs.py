#
# Tool to convert BIN+CFG pairs to an ECS file
#
# About the ECS file format:
#
# This format allows for Intellivision ECS-style bankswitching titles
# to be specified in a single binary file.
#
# A bank is defined as a block of 2048 16-bit words. Each bank supports up to 16 pages.
# In 16-bit address space, there are exactly 32 blocks, with the first one residing at
# $0000-$07FF and the last one residing at $F800-$FFFF.
#
# Since ECS banking operates on 4K address chunks, paged blocks will typically appear
# here in pairs. The 2K address chunks are specified to allow static and RAM blocks to
# be sized that way.
#
# ECS file format:
#
# [00..06] "ECSINTV" hard-coded identifier
# [07..07] ASCII encoded version # (currently 0)
# [08..0F] reserved for future use
# [10..2F] block types for each of the 32 contiguous blocks
#          ASCII encoded types:
#          'S' = static block
#          'P' = paged block
#          'R' = RAM block
# [30..6F] block details for each of the 32 contiguous blocks, 16-bit big-endian word per block
#          'S' block: reserved for future use
#          'P' block: each bit represents if a page is used for this bank (1=used)
#          'R' block: 8 for 8-bit RAM, or 16 for 16-bit RAM
# [70..  ] Static data blocks, exactly 4096 bytes each, ordered by address, with big-endian words
#          Paged blocks for page 0, ordered by address
#          Paged blocks for page 1
#          ...
#          Paged blocks for page 15
#          Note that only the used blocks are stored

import glob
import os
import sys

header = bytearray(0x10)
header[0:7] = 0x45, 0x43, 0x53, 0x49, 0x4E, 0x54, 0x56 # ECSINTV
header[7] = 0x30 # version 0

# 32 x 2K banks = 64K address space
MAXPAGE = 0x10
MAXBANK = 0x20
BLOCKSIZE = 0x800
MAXADDR = 0x10000
BYTESPERWORD = 2

def convert(binfile, cfginfo, ecsfile):
    pagedata = {}
    blocktype = bytearray(MAXBANK)
    blockdetails = bytearray(MAXBANK*2) # word array
    for key in sorted(cfginfo):
        info = cfginfo[key]
        loc = info["loc"]
        if loc < 0 or loc % BLOCKSIZE or loc >= MAXADDR:
            print("Error: Location must start on a 2K boundary")
        startblock = loc // BLOCKSIZE
        endblock = (loc + info["words"] - 1) // BLOCKSIZE
        
        usetype = ord('S') # static page
        usepage = -1
        if info.get("ram", -1) != -1:
            usetype = ord('R') # ram page
            blockdetails[block * 2 + 1] = info["ram"]
        else:
            bytes = info["words"] * BYTESPERWORD
            binfile.seek(info["offset"]*BYTESPERWORD)
            if info.get("page", -1) != -1:
                usetype = ord('P') # bankswitched page
                usepage = info["page"]
            if pagedata.get(usepage, -1) == -1:
                pagedata[usepage] = bytearray(MAXADDR*BYTESPERWORD)
            pagedata[usepage][loc*BYTESPERWORD:loc*BYTESPERWORD+bytes] = binfile.read(bytes)

        for block in range(startblock, endblock + 1):
            blocktype[block] = usetype
            if usepage != -1:
                blockdetails[block * 2 + (1 if usepage < 8 else 0)] |= 1 << (usepage & 0x7)

    ecsfile.write(header)
    ecsfile.write(blocktype)
    ecsfile.write(blockdetails)

    for block in range(0, MAXBANK):
        if (blocktype[block] == ord('S')):
            ecsfile.write(pagedata[-1][block * BLOCKSIZE*BYTESPERWORD:(block+1) * BLOCKSIZE*BYTESPERWORD])

    for page in range(0, MAXPAGE):
        for block in range(0, MAXBANK):
            if (blocktype[block] == ord('P')):
                if blockdetails[block * 2 + (1 if page < 8 else 0)] >> (page & 7):
                    ecsfile.write(pagedata[page][block * BLOCKSIZE*BYTESPERWORD:(block+1) * BLOCKSIZE*BYTESPERWORD])

    ecsfile.close()

def parsehex(hex):
    if hex[0] == '$':
        return int(hex[1:], base=16)
    else:
        return int(hex, base=16)

def parsemap(mapinfo):
    if (len(mapinfo) >= 5 and mapinfo[1] == '-' and mapinfo[3] == '='):
        val = {"offset": parsehex(mapinfo[0])}
        val["words"] = parsehex(mapinfo[2]) + 1 - val["offset"]
        val["loc"] = parsehex(mapinfo[4])
        if len(mapinfo) > 5 and mapinfo[5].lower() == "page":
            val["page"] = parsehex(mapinfo[6])
            val["key"] = "%4xP%x" % (val["loc"], val["page"])
        else:
            val["key"] = "%4x" % (val["loc"])
        return val

def parsemem(meminfo):
    if (len(meminfo) >= 6 and meminfo[1] == '-' and meminfo[3] == '=' and meminfo[4].lower() == "ram"):
        val = {"loc": parsehex(meminfo[0])}
        val["words"] = parsehex(meminfo[2]) + 1 - val["loc"]
        val["ram"] = int(meminfo[5])
        val["key"] = "%4x" % (val["loc"])
        return val

def parsecfg(cfgname):
    section = ""
    items = {}
    for txt in open(cfgname, 'r').readlines():
        txt = txt.strip().lower()
        if len(txt):
            if txt[0] == '[' and txt[-1] == ']':
                section = txt[1:-1]
            elif section == "mapping":
                data = parsemap(txt.split())
                if data:
                    items[data["key"]] = data
            elif section == "memattr":
                data = parsemem(txt.split())
                if data:
                    items[data["key"]] = data
    return items

def parsebin(binname):
    print("Converting " + binname, end="")
    cfgname = name[:-3] + "cfg"
    ecsname = name[:-3] + "ecs"
    cfginfo = {}
    if (os.path.exists(cfgname)):
        print(" + " + cfgname, end="")
        cfginfo = parsecfg(cfgname)
    print(" -> " + ecsname)
    convert(open(binname, 'rb'), cfginfo, open(ecsname, 'wb'))

if (len(sys.argv) <= 1):
    print("Usage: BIN2ECS <filespec>")
else:
    binFilesList = glob.glob(sys.argv[1])
    for name in binFilesList:
        if name.lower().endswith(".bin"):
            parsebin(name)
if getattr(sys, 'frozen', False):
    input("Press enter to proceed...")