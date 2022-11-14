#! /usr/bin/env python3
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

import os
import sys
import hashlib
from pathlib import Path

header = bytearray(0x10)
header[0:7] = 0x45, 0x43, 0x53, 0x49, 0x4E, 0x54, 0x56 # ECSINTV
header[7] = 0x30 # version 0

cart_data = [
        { 'hash': '8f311c3a49e8a660b8ed2cfae369e27915fe841b8d067784e96f6605552daf4a6a556978781f26c981b13791b726288d5139a3312b517153bbc1b869fa302df1',
          'name': '4-TRIS (2000) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '171d804e7dd7a0eba774bcfa897d4c79cba195294b017f4845a2f42bb65c8754a2e605e33b57486ace35ce7920d3b363ea87d071d1ee88c018b05a1621c12109',
          'name': '4-TRIS (2001) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '7b73837270ea5a4ca85eae016e7131e2b48e2891f1536a1274cac9bda5e190561caf126b6ac18d780da30d5ab8779c39c2908eb19e966d31240088698916bd94',
          'name': 'ABPA Backgammon (1978) (Mattel)', 'mapper': 0 },
        { 'hash': '5fe40df80d91282070ae504dca0aaa493c17c35b4b40716679894c769e9c3485b75570fb31e1de3684c9653999a00f774b5d69336d297950d1cd73fe63890754',
          'name': 'Advanced D&D - Treasure of Tarmin (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'f6d86f9e414fa0bc283c067988893bcfc3927e3440565b863a04226dfb177cce54282bd61a662f8b5fd7c541abcdd6cc473a8472d209adbf6eeb53113db7cd2e',
          'name': 'Advanced Dungeons and Dragons (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'd5732eb408eb0ee2408fae6a4b853b2848cf06462d7ea48afcad732637dbf5c12810787be7d81feed83dd3447327de5c2a12788f8bb87e1bd2773e6f5d090026',
          'name': 'Adventure (AD&D - Cloudy Mountain) (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '9611467efe0cec41fc37d15400c79c0a5ac0d87e600ef8d3f449e2820f785b2b7993398cc385bdd214fc0585e6ccf8f53d0c83fb09befc32aac38ead91b69e54',
          'name': 'Air Strike (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '979d448901b00429564bb57e1985ca03ee800509d9d08fa149f4accecd8624d9ec32bccbaf54c275dc6ec3301d3928eb27f47fda09f647e735e0481aaa3084bc',
          'name': 'All-Star Major League Baseball (1983) (Mattel)', 'mapper': 0 },
        { 'hash': '1cc1d8cafe7408814667fcc3fffc7f865068bba155643eb24700a242421adf4988c6dc32a6d6fd81f423983e50a36bd1bdb5938f2f1524f8d300d30615dc3c78',
          'name': 'Armor Battle (1978) (Mattel)', 'mapper': 0 },
        { 'hash': '2ec3034f60cb1585ff3daa0fc541be22b279a95b459686903ab60ae873afa3770441a9a078673dbc6e1514bfa80a5819c72c1aa1716d856f937b11ffb7c71069',
          'name': 'Astrosmash (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '58d89805a1f4543368ec8b2a77aa9e4a743a8f5c2aefc88d50febbf26df666383a134cf3c075e13a1a2c73b0264c62b2175317804884b528023c072b7a0e59a3',
          'name': 'Astrosmash - Meteor (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '60f7ebcfbbf714c40f7e03fd2db28ae2a148bd4b56c4d80ba544ee9fdfd274dc663f3b13076a723d4aa5913f07586e45dca1cc111a37320a20201da6d444d8eb',
          'name': 'Atlantis (1981) (Imagic) [!]', 'mapper': 7 },
        { 'hash': 'fb5d1b25548d47a0975b4e652c36202c88a350c5f4c309d0bf41b027708c1112895a39e8da7b54c516e6426c9143c615cac1e70ad796b9d3fa00e755334d2530',
          'name': 'Auto Racing (1979) (Mattel)', 'mapper': 0 },
        { 'hash': '8263ac799e822c578f0f43d57c379e47f1d39e66c8410532487cc2fec27d423e01f9f1467ce1a2d11767a406e561ddab2389394718dfb391f256839d345cee22',
          'name': 'B-17 Bomber (1981) (Mattel) [!]', 'mapper': 0 },
        { 'hash': '89e54a934c778a5478ca77f47096d884b6361603ee857e1e35319a0a8a670bdc9ed0d248ab0c383d220e08480a84d5f907712137b73aa43f89ef14bda93212e5',
          'name': 'Baseball (1978) (Mattel)', 'mapper': 0 },
        { 'hash': '4fa46f9db58fe3379564c0eed8c1fb109501f67d3265358bfe736346dc945762d62fd8452c016010322993833488969b06d25ec0bf6724bc5fa13f91aaed284f',
          'name': 'BeamRider (1983) (Activision) [!]', 'mapper': 0 },
        { 'hash': '00c93885704cc1d628039d4d2446dfe0e7d25f37214c7cae40b5fb4ff9a0165e9438f565ea6c45b683a5a81e058f7806eb2b5210b452ad1372d831e2464c960f',
          'name': 'Beauty and the Beast (1982) (Imagic) [!]', 'mapper': 7 },
        { 'hash': 'd337d3f98231e14c98d9142dcc311d10c5db681b59ba88ee7bedc093bf16ff62f057fb8e5b5499683feb5f47aab5786e7ab481e88e8a2f3bb6b9eb25a7a51be7',
          'name': 'Blockade Runner (1983) (Interphase)', 'mapper': 0 },
        { 'hash': '7d403f615312297a0e2e16a9dbce5fda588da6fd42661511db5a709dcfeb5825dc89ebb87446448220e56f87d6d4668be442b638dd8f49eab672792d9c8b156c',
          'name': 'Body Slam - Super Pro Wrestling (1988) (Intv Corp)', 'mapper': 2 },
        { 'hash': '9afcd2f3e18b854fb8f15dd2fa36ec44a18d7fadd14fadffbc61df465cc292254c0d1b74044576a16c3bb23892cd3cd9d283aeb4e5d5537d77b65040c0611474',
          'name': 'Bomb Squad (1982) (Mattel) [!]', 'mapper': 0 },
        { 'hash': '240335fffca0d75e8a4dadc7667264f27954c1785c0707301c94e648db0b9b5b32e0d8aea460d5287499a7def2372765d3a8c5bfdacf866325af23d30e52426d',
          'name': 'Bouncing Pixels (1999) (JRMZ Electronics)', 'mapper': 0 },
        { 'hash': '6dc197c2940aad5f748d728b3342b9f3973391f37fd19e226b9e587d4b65d98cc68c7202601f0937d897922b335643f3655bfe2c81228ff224d068b6e39cc178',
          'name': 'Boxing (1980) (Mattel)', 'mapper': 0 },
        { 'hash': '45bbbed06a479863f7eb5c7c46abc30d039e8b656c7ac2a0a4de6fee6b52447f27667572f558fc6664cd6d6ec4f751d0de31444985f6d937abc83aab3596d3c4',
          'name': 'Brickout! (1981) (Mattel)', 'mapper': 0 },
        { 'hash': 'da27f2d0e72a7730d374c69646a335ccc6c086363d29febeca048c0b835a1c0b1ad9bf3fda80c7df5097b830f0f6c72b59242a26fb54e8bd89889cebf2cc86b7',
          'name': 'Bump \'N\' Jump (1982-83) (Mattel)', 'mapper': 0 },
        { 'hash': 'b284976f06ad2dc04c43bf392139d69fb771c0149e61222f803ba8de1b82eb1182b421a5cd505fe778f3424b89015d3ce86418fae5d2912f4492ef885cfd23be',
          'name': 'BurgerTime! (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'bf6f4a388c8ecf8ce1c3107ae485df3886b6383600d44fcc9d94dc2a3e02437d38b1884e92c1bfa0ab5e4e2e0e01abb0fec222ab9d20e297da3b408d9e484c97',
          'name': 'BurgerTime! - New Levels Hack (2002) (David Harley)', 'mapper': 0 },
        { 'hash': 'dc658da04fa7a5fa5b87190326362de763bfa42957891576d4709e8350d15f7b4ae6d0a67421db0c27287b878fc1afd52988986e52aeb8142139eeefd460d836',
          'name': 'Buzz Bombers (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '159da66cd503e84f7010ccb765e7098dc6ed1924c4c5eec2736a7f333a21079b0488317b0dda52a468e798e527a775e7307a671a0643e1b90beec2d1e47eacf8',
          'name': 'Carnival (1982) (Coleco-CBS)', 'mapper': 0 },
        { 'hash': '16d240134729bb20633ebfd31883f09fe0686f41333f788d2738f4bf272abf5d6b49e0fc0c748c529d56f139f30d29e1344b2bc66696f1df7faee145d2639231',
          'name': 'Centipede (1983) (Atarisoft)', 'mapper': 6 },
        { 'hash': 'a934613982ebb623d159d4bfbb2b6c40daf9c7c2fd26e5d1ba03b7a4e2bbe864aec25792a68566424f24decaab7b6282b55c88ca3ede42b731cd8b9ef9228b8b',
          'name': 'Championship Tennis (1985) (Mattel)', 'mapper': 1 },
        { 'hash': '498295068779840740d21911066534e00fdabef09cba72242e3b3061415a393e9438b6715ce8bffb5919cc91a4af02b5359dccfb608159ff7bda7cc5e2a9952a',
          'name': 'Checkers (1979) (Mattel)', 'mapper': 0 },
        { 'hash': '1c01a520edbf5974014a2eb58eff7494289cb287721fc4fc550c8a079c7747f0c01c2945e878bc2cee02f26104450d29bc70baf7c177293ac1a23848683e9e38',
          'name': 'Chip Shot - Super Pro Golf (1987) (Intv Corp)', 'mapper': 2 },
        { 'hash': 'ce819ab2dc6a79129f4096bb9cb9c9c1ccff2ff9232d61d470d6c4500aa61f69fec3c6fc4743c0db8d261075747ac7f42cffaa35fa470383f566b19b617253a7',
          'name': 'Commando (1987) (Mattel)', 'mapper': 2 },
        { 'hash': 'ab665d7d47286f0133382d28b3dc2ff1b67c7b346a13a4369674a20bb3760fd390ada7990b82ed8ea50b705b51e7514c1a67f5857f9bedb3e9fc9d3f41d41059',
          'name': 'Congo Bongo (1983) (Sega)', 'mapper': 5 },
        { 'hash': '9691a5a89a560ccba7daaa25c21764ac454169fbb74176aeef680c76130f97246f00ff1cf05c1d04393b5ea60e3911435a4aaed796344f80daa67e983d62d602',
          'name': 'Crazy Clones (1981) (PD)', 'mapper': 0 },
        { 'hash': 'dabbd6d2ace4aa55bcfb390a743b81aaa5ef04b56f940f0352a6ba3f5a51817f42e9c0ab75a7c511cd9079a8251c2f9d1a13cac3c3bf35a8e666dea7de25a39e',
          'name': 'Deep Pockets-Super Pro Pool and Billiards (1990) (Realtime) [!]', 'mapper': 2 },
        { 'hash': '84c6bc4f6ad5322dc8765c215a53da537cba4a439edba97646fd34393caefb9b2eca88f0d537824663f5372da7a46aeb93507a79799f0576dc14739791d54861',
          'name': 'Defender (1983) (Atarisoft)', 'mapper': 5 },
        { 'hash': 'da251c44e4aeb1fb608ccf79feb80310c16094b220f169e77863303a81916e9ca0efe93071b2461af6ff88d68c78390d4e30d270d89383a33fe0da5a00a6bba0',
          'name': 'Demon Attack (1982) (Imagic) [!]', 'mapper': 7 },
        { 'hash': '43185308eb58aa052c46a7a5fdea7a38fc46f0684bf289e8cef03e5525a291250a2d6eb02b687d0c086a5617bd993ab6429f3b9e494a8b1282e6dd3cd691e34f',
          'name': 'Dig Dug (1987) (Intv Corp)', 'mapper': 5 },
        { 'hash': '393ba218f538ca604564c56cc1e1b625305ed0d09d4f2b1ba390900074cda4e1d7e2ef58760cc08dbdd52baf1112b939b0e13c5d353fba94846ab9d527bdf16a',
          'name': 'Diner (1987) (Intv Corp)', 'mapper': 2 },
        { 'hash': '512f2c0cd1d494711c27bc9ba3a5630e22b7048b46a054dd4858ead9a1b07bc673c58f78cf79d0aa3568bbc685dde7133cb56023e80aade3352fea5cdf3dbb7b',
          'name': 'Donkey Kong (1982) (Coleco)', 'mapper': 0 },
        { 'hash': '33dda5499b5035be597cccc886081caf05a95d27550dd82e50e134d168b6a69b9a511cd57f5b18833d57ea6dfbf36d749883340cb781964cbd85d9cd160dc52f',
          'name': 'Donkey Kong Jr (1982) (Coleco)', 'mapper': 0 },
        { 'hash': '5263ba2940b0908b156b2ab5ef0bc9eea0d24b300434b490d2aeec24df373cc34e48832c22c3b8ba8cf8804ea5ac9f61ae13664f4580a173d46a10b11769ff8f',
          'name': 'Dracula (1982) (Imagic) [!]', 'mapper': 0 },
        { 'hash': '84da217b61d1dc48d05881888081753bb047d7b07d5e403bd46d55dc00be4f36f11b8a468c8b0b227eeacb40cb7f0036dd65d8ecd719300b07e6671f1ad146f0',
          'name': 'Dragonfire (1982) (Imagic) [!]', 'mapper': 0 },
        { 'hash': '5bb519f478486eda032e07189f2f9cb954cc493ea4000889a75b479edc572544adb5047183a5c77daf318b4dc936784626cc98168d3d28bd2cf57284430b9b21',
          'name': 'Dreadnaught Factor, The (1983) (Activision) [!]', 'mapper': 0 },
        { 'hash': 'b97d728950ef7df7c385b079171be5e2aa809276d611d5e0934e0955db7a0d8450e1f17db4f828fd2653b5d70d2d19c05566be79b78fcad1970dff76b08546e4',
          'name': 'Dreadnaught Factor, The (Prototype) (1983) (Activision)', 'mapper': 0 },
        { 'hash': 'fd8f702e749d39641f4a1c9744fd5c2ec606e59cdb9bd38b9bb7c2d8cf011d1a778e301046270ecbfbc5af49d9614f834cf023069cfd980cb8398e5b8d93c562',
          'name': 'Duncan\'s Thin Ice (1983) (Mattel)', 'mapper': 0 },
        { 'hash': '2be299bd05f509c798f2f88f7769cbcbda89bd3a0027a16d5898136235117fd1cd68e02e9ecc52af5c70c4a5f375ef795ba206671ab1bfeff8840310c2f1696b',
          'name': 'Easter Eggs (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '427346d9d490739e771ac6d50adbb2bec5e4cf6a30df982dc7b7196259a978e955c8602e8c888fa3bb306efc3086c1460a8e547237a7f504655da4b225d28550',
          'name': 'Eggs \'n\' Eyes by Scott Nudds (1996) (PD)', 'mapper': 0 },
        { 'hash': 'd2d1880cbcf8209d3b6acc2933f1cf8dcdf71c36c50876e4bb67a784b90d0a30fdeb12dfaa7cbf9846e29820aeb31069a5dc5c6b89fdabe290b04ddf4094e92c',
          'name': 'Electric Company - Math Fun (1978) (CTW)', 'mapper': 0 },
        { 'hash': 'b4b6f642d9f88e6000a78b5640027184ba94c3dc39065ebd992f155fd8a1ace1cf7602870b4e3532a449454c6ea507229a4006c3c1247c141961a2275eb8c46f',
          'name': 'Electric Company - Word Fun (1980) (CTW)', 'mapper': 0 },
        { 'hash': '0305ffd635712a15d7d86bcfdca27ffeda6130df6123ab70eeed38f059dd2f931039d3b8b4008da73fcd7d8f829b03677f5f8a041d743c916f45b069ce380fd2',
          'name': 'Entertainment Computer System EXEC-BASIC (1978) (Mattel) [!]', 'mapper': 0 },
        { 'hash': 'ef24edc7d7e3c61d9308b34700d1cdcea159f483b115d77102f3430375f414b55c5a2c84ebdde90e4745e88b201d2979c3e8ff121d082597ca6db5c4a90102b8',
          'name': 'Executive ROM, The (1978) (Mattel)', 'mapper': 0 },
        { 'hash': 'f613469c27195a5b32526cac2c60bc93a28ef3da6cd60d6199a3d45aa129146f48b4bed46ab564c6dd9aa9d1f15606cea37c09d5c85e8bf30e4d27823b5af3df',
          'name': 'Fathom (1983) (Imagic) [!]', 'mapper': 0 },
        { 'hash': '6feec8e0e38e68e8e12a1c636d763ee773559f50d75ef87ec04fd5c09b8c9d727da71c2f9278b83b51ed513eac76f4d944366fb304e0a87e3b2db913a4383d4f',
          'name': 'Frog Bog (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '25f1761ec4792af1822b7cdb0b0d08249a1563a0278d3d3b5c9fd58852fc0ff4af4e1578de11d6615648a699448c55ff3ad355495f3554b462154eb79e481d87',
          'name': 'Frogger (1983) (Parker Bros)', 'mapper': 0 },
        { 'hash': 'b7cd33fb153f3cc2c3a63d1ad9a8a4dae95b3ffd2263e03bb59391e28cae664779364fbfc61a46c48fb467db2626fa217cbe942a022b2e38f2b330ce01eeac5a',
          'name': 'Game Factory (Prototype) (1983) (Mattel) [!]', 'mapper': 9 },
        { 'hash': 'b8c0043493742d1b6b4c7df0617348f78e1810c8b6f71a8223b656fa2f0b004761701f17506edbafafd4e881a91431acab51bf8b3afd38c3acb9c5d82f81d627',
          'name': 'Go For the Gold (1981) (Mattel)', 'mapper': 0 },
        { 'hash': 'e361a6bf01f1edcfd025298939b2029d79abecbb162ec7edb9891cdf36ab4c8617fae51c2a1eec4d5ab3dce897475582a92bf4dd02c307261f60dea7f8701f02',
          'name': 'Grid Shock (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '902b66632972d2991057a7f077806520b873c1e7266ef2dca5e1e419b09fd65998100ea7f0fa1ab2b6a39dff94b785af31be74220244b83c7ae0a120ddcb1c9d',
          'name': 'GROM, The (1978) (General Instruments) [!]', 'mapper': 0 },
        { 'hash': '242ba763616fb9485e5446bc5d34ba349bff97a2506bcdd230f755ce52b643698acbd93699cdf957d3dbaa5aa8a84400c607b5a4925801d943723474879f42e4',
          'name': 'Groovy! (1999) (JRMZ Electronics)', 'mapper': 0 },
        { 'hash': '6d293b3abfe731887535b691c92473cf30b82d3d8a5fc622e96ed3395e9fb12caac6409c4492c33b735079f054a00008d298a01bdb3709a5719109938ecf0b60',
          'name': 'Happy Trails (1983) (Activision)', 'mapper': 0 },
        { 'hash': 'e17224f52dab20ff84375130cd6811a1ac99c288ee5cce2195d99439de57528a7c6cdb65f0b4a64437381c55d70713c9fe241640c5fe0076537c43799b70faf4',
          'name': 'Happy Trails (1983) (Activision) [o1]', 'mapper': 0 },
        { 'hash': '65aa05ae729c551b07e4e994f64d3ae676fad0f25629f6af82f09c6e9028d34fd0d35d743deb10e03dcfc16ffdfcb9510491f32ae5cc9f3f6c08e625695e0c41',
          'name': 'Hard Hat (1979) (Mattel)', 'mapper': 0 },
        { 'hash': '3815dc5d8f4ea65399a5223aa05bcace0450cc0ffa06e21bb04867f6842fa7778dd75be9f5be29d39ddbdba14f2707aa48d461097e1092d2e37e20e7216ace34',
          'name': 'Horse Racing (1980) (Mattel)', 'mapper': 0 },
        { 'hash': '3eea46c7b42737ed9cd209dc9c66fae089a105b54c4ba2db2ed61e6105313760d9e84fcb644a1f61e24dbc85133ce2a2b6ef17e3a4714064f3ae612a11e6762e',
          'name': 'Hover Force (1986) (Intv Corp)', 'mapper': 2 },
        { 'hash': '48b5104786919d54e0acd400d284db61698625bcdf3f903da79b61c6e43faa3051f7779bf467e7afacdc457992d1ee66fb74aa74ce3a577fe974a4b56a2fad63',
          'name': 'Hypnotic Lights (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '9a8b88bcb1b52d434b9967fcbed50a7f0c93397a22d6a6674832c55e31bfeecfe75437f2c2391704dc8e627f5381c91134a0901e4c7c8d0f212275270bb965df',
          'name': 'Ice Trek (1983) (Imagic) [!]', 'mapper': 0 },
        { 'hash': '01e723de621c5957f8760f8bb506dfd36da3ce9064c2967974325a20eaf425c9803a76b6ff8388d9635415791870f36290dd8478fbb6b050984881be1f281562',
          'name': 'IMI Test Cart (1978) (Mattel) [!]', 'mapper': 0 },
        { 'hash': '47ba1d90f722c7bddec8b425815f4686bee9f0dee094f4c2b8d908fda7894b9db046b98c90a5af6ca4d2e904534089dfed1ca397aaa556e6b9d9ee67fa3e200c',
          'name': 'IntelliVoice BIOS (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '6b135fe4cefa78bc13283d22473db7d37ee78611e322f9e895c0fe949eeb46fb13c722b54fd5b67efcfde3b5560fb1fdcc29fa4d932a57fc9f46b84ca4d01514',
          'name': 'INTV - Intelligent TV Demo #1682 (Prototype) (1978) (Mattel)', 'mapper': 0 },
        { 'hash': 'c85dc05cc10f7fe168354cda618515e4908d2d6174c454c718ce1b10e2d853a06b5643fb608deddb836c0bb2bb0a7ce97ee3d6d01216cfc4a41c5e7ec9a40ca4',
          'name': 'INTV - Intelligent TV Demo (1978) (Mattel)', 'mapper': 0 },
        { 'hash': '09d43522f22c0cfe7c3ca5d8fb884e3ae3044da2e82d45b0696ae7b88a6c67e1ee722b084b89798410db703f33744e18ee69728a0a68157640c29f38b25d7277',
          'name': 'INTV - Intelligent TV Demo #5853 (1983) (Mattel)', 'mapper': 0 },
        { 'hash': 'ebced06c98f8f1c0fd11487c05b4045c8243b8877473f8750c47f2a73d8fd80672b312bc94f11b85817192ee1cd5ed38c0c61d14d0de2d3462cc2b6fd24c9f29',
          'name': 'INTV - Intelligent TV Demo #5853 (1983) (Mattel) [o1]', 'mapper': 0 },
        { 'hash': '75f647e722609f953f8a13bc4fcbcf293a6d0e37ad62998cf0988d91ea821741e49d8ce704bbc2d551fe8cb1bf63e666c026216bc9b106929b8665fe670d4694',
          'name': 'INTV - Intelligent TV Demo #5932 Revised (1978) (Mattel)', 'mapper': 0 },
        { 'hash': '450f5c66ac5e2078ccd79513a901fd7045cd5529bd36928248fdb306606302ec005c80901da128ec20211c48e15f7b78f78bf089d32d8234b8840c02b2eec1c4',
          'name': 'INTV - Intelligent TV Demo #5932 Revised (1978) (Mattel) [o1]', 'mapper': 0 },
        { 'hash': '40bf8392f5556dd704423f5c7a6b91d91236201a09048702f73679b84fe97af5e50b0b92906ac04afa2cf3f81c3c22f05089820097901444ce0a37a831ded612',
          'name': 'INTV - Intelligent TV Demo Intl. #5859 (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'ea4b51536637c32b14f2064868398a7386ad445d5dc18dd4fc37d9c0e29d2196b4d636be6918044c9e76fce7f6a449f019466591ff1c6ddc4b366bf26b7a5f2d',
          'name': 'INTV - Intelligent TV Demo Intl. #5859 (1982) (Mattel) [o1]', 'mapper': 0 },
        { 'hash': '5a7282e009a48fa63e73a3f8341a6e5bd45279be4545dd704ec28fd2b77899ac2254247cd27061369c8ac78d5aea709dd9e2aed56f89cc845baafa0bdad2c270',
          'name': 'Jetsons, The - Ways With Words (1983) (Mattel)', 'mapper': 0 },
        { 'hash': 'bd13c8cc193460f880c59a4f1c69f206752f1817040211b22cfecfe9a9cb790958e270961e7fb9c6dff017fb05d64bfc5f22673550e7f8817de02115ba4e0068',
          'name': 'King of the Mountain (1982) (Mattel)', 'mapper': 1 },
        { 'hash': '13a4dc08db93c038946e422840890ab376ce8b3336029e657d32d8cca880e2a4b57cd4e2bdf3bc9724f58c24200420eedb653e24494ac05df2c6293be93bbf12',
          'name': 'Kool-Aid Man (1983) (Mattel)', 'mapper': 0 },
        { 'hash': 'bb905ad999eab70e084b77bfd388351709e0ef887f8fda5157a743f3975a18b8e6c6d6775e1eb3cf9988ba02c00fa2c2a2f38feb1ebf23435d71e5de68f2bf50',
          'name': 'Lady Bug (1983) (Coleco)', 'mapper': 0 },
        { 'hash': 'f5d33f939155301ea278a6adc606a4a17b9fc11aea55e0dbc15fbd0e49455daaba5249071debc0c63896b51c993b02dc17facb517f90f351d9c9bd6ac801532a',
          'name': 'Land Battle (1982) (Mattel)', 'mapper': 4 },
        { 'hash': 'd62b3c0c4858d24b4a00ea394d4b1de8f3aa11fb9568701f5ebb1c6b34a8edb9b07fe9cec0012b5f8f0dca172ac1241ba8e353c8b9575039191aec18daa944fe',
          'name': 'Las Vegas Blackjack and Poker (1979) (Mattel)', 'mapper': 0 },
        { 'hash': '31741c584996eec435644889b47b66733553a00a4dd7780b3ce94f12d175d9a06e1e4ab1e4baae1a126e129583603f3b3481ea304ed4843fc21e6e10f4b27147',
          'name': 'Las Vegas Roulette (1979) (Mattel)', 'mapper': 0 },
        { 'hash': '0d720d9e4a19e9cb3d22e49ad7b2d27f99fa19e494c9c844358bd2c98162cadcbb4b76cb81ca6aa66b2668ef2dc1ee6ea8c6d9ff18627af6d08ac5a7c310f7a4',
          'name': 'League of Light (Prototype) (1983) (Activision) [a1][!]', 'mapper': 0 },
        { 'hash': '7abbbcd38cc05b7d1d2bdb4eb7c804676a6567933ff34431c4c03c75e2d2b590a6b07f944974d47edeb95369b795c9db3d6b058ecf755fead4ddbb7a0f382193',
          'name': 'League of Light (Prototype) (1983) (Activision) [!]', 'mapper': 0 },
        { 'hash': '99da7987b8b199de0d0c92b39760c46d6657ae296590b24c5000bdf9dec9bfd25df4b6f8309708273a2ee8be7cffa92d02e97c4151e21b1ac115d68ef24f3e24',
          'name': 'Learning Fun II - Word Wizard Memory Fun (1987) (Intv Corp)', 'mapper': 2 },
        { 'hash': '5847e2c9366a8ab66c8582fb3f92db848d11c6eb9af3a584cc144e9f7567b168d04c8f34d7f6d193f90b2bbda6272f676544c88e55365ed44fd91ec7a0d766f0',
          'name': 'Learning Fun I - Math Master Factor Fun (1987) (Intv Corp)', 'mapper': 2 },
        { 'hash': '1d8db044e870af73f433a5ef8e51aa8fafe896dae01c584c7ff0f4d7020cf4864f8b45d2da2d814b8a71968139bb456b73c5651c8a72bfd8ded7fd427a00e3ae',
          'name': 'Lock \'N\' Chase (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'e8a1d0ea0be7731faa57b2fd4073f76f95cea46a3e608570a3102c4697b512d3c05821ee0f77ddb1e59b8bc13e32f9689540ba8d7e8f003ff1202257afd209e5',
          'name': 'Loco-Motion (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '39b63f469d827bf3d31fe82169027725a37f03b9fd73c05b6a62dcafd373546119fc75ad877bdcf6194a6e543f4517a70aaec16c67ebc9455f09ab370f469fff',
          'name': 'Magic Carousel (Prototype) (1983) (Intv Corp) [!]', 'mapper': 0 },
        { 'hash': 'd1a70101ef19f1f64ab4a1e0449014c47fa0dda72a4cd5e62926286fe8222f4c5496076259761e4a38ac2633ac4b4e56cdcf68a2169b612bab7bdcd7b17428a1',
          'name': 'Masters of the Universe-The Power of He-Man! (1983) (Mattel)', 'mapper': 0 },
        { 'hash': '93b795748049cd83c5cd0a87f361b17472067041dca3c6b4a3eff680dba2d6914d6ab3c33db70d2e2c6d5844a90e27df93329c5fbfec6e774edbc31f0287e519',
          'name': 'Maze Demo #1 (2000) (JRMZ Electronics)', 'mapper': 0 },
        { 'hash': '58af6da8add1572c3394c6c08706813f8f29ecbe69d650b3a495dd18640a0ca73519ce6d032446f6ad2e3d161e48ddcd9f80f04c5d279916e337de7ef8ed422e',
          'name': 'Maze Demo #2 (2000) (JRMZ Electronics)', 'mapper': 0 },
        { 'hash': 'c3f48489670e4e7a10b4f9058b4ab3b31653330b5b50128064edb026551c051fe70bfb3946916e31c5ad84fb030e818f6e8449345ea2b0db11b1fcb147feb8e1',
          'name': 'Melody Blaster (1983) (Mattel) [!]', 'mapper': 0 },
        { 'hash': 'ed980144cbd59ae4dfea1c187bbb41ddfe9fbe5e04e767a7b0090864453fa947afe4088f17edd8872940b479f21207cbbad768767dd6ea9d1081b81ff4ff8a32',
          'name': 'Mickey\'s Hello World (1999) (PD)', 'mapper': 0 },
        { 'hash': '6b387dcb14baa23530bb8a8f25e98e5ecd4ceecbc7208f5ac78570e72c99224c2b725259b29e3f02ae270a11f04b25b06db9099221bd9a14e03c8c29c93da455',
          'name': 'Microsurgeon (1982) (Imagic) [!]', 'mapper': 7 },
        { 'hash': '369def3e15300fd0c8bd9f80a86b16b0617139364fe47860469a934298c5079697fb369273817e184331c0c5c82b77f5a7ed6ac4f0e07faa9efcab05b5b2cd0f',
          'name': 'Mind Strike! (1982) (Mattel) [!]', 'mapper': 0 },
        { 'hash': '67dc1b1fba70dc802cee89e89c575264068bea0b208219fc848ff984d3948ce3aa1626f12832ca63c3234c9e7ac7abcb86006bf361b67ddd4ae62271f777d49c',
          'name': 'Minotaur (1981) (Mattel)', 'mapper': 0 },
        { 'hash': 'f05c46f089e48f18237cbbb0a80ae1d2b54ddb164fd5ea29f1431f8b84cb82ce5d6d88408c8582b2e46c76ac26e5827deb78c8bee7bb61952ddd54618d5e5b61',
          'name': 'Minotaur (Treasure of Tarmin Hack) (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'df59c847902797a5f45d0fa24d44e25c05cd3d0620c4f1b0a07fd7bd8a1f3bd6911216c8eb2edf941ed643c1b0d519aa1512faf82744b845216d7f3f0971c7a9',
          'name': 'Minotaur V2 (1981) (Mattel) [!]', 'mapper': 0 },
        { 'hash': '1d24a0e9edc46b2e86c6112049217eb2509d8fd2ac0301ae80cb082684e4c31211dbab48e1ad3f1030838fd2e45be1b3118e581be8500055dfe3ba8442cb8cf6',
          'name': 'Mission X (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'c0e73d7e815edf5703bcf62bebf85554581e18879e3b58fcf332913bf7238a6495c8178d2f98c5f5a3301eb0e8f568ed3ed3c93b7bfbebd8da18ea4865c9eda0',
          'name': 'Motocross (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '1ba942aff49495145fb82a281d6e9fdc0258afb3e4ecf6acd980744fc192155cccc0f5596ead7eeb11dd18a86d9d1a58938b10cb501c9fce10bd7673a6b52932',
          'name': 'Mountain Madness - Super Pro Skiing (1987) (Intv Corp)', 'mapper': 0 },
        { 'hash': 'f5915c75a4bebe95b4a507c2eeaba5759a557d75b3756d3004eceae14f71cd81b38d8ba1df884de97413f8c94edbbf62e7d74cf921f9fc33685dece751f53a17',
          'name': 'Mouse Trap (1982) (Coleco)', 'mapper': 0 },
        { 'hash': '9c4f0e584453fac88ac27387e27c3f370a195a350536768669fac44947caa1aae40b2cf099b10c3352b0cf338fc483e21c3e8558967ecaff1f92c24161f10404',
          'name': 'Mr. Basic Meets Bits \'N Bytes (1983) (Mattel) [b1]', 'mapper': 0 },
        { 'hash': 'eea3692c629d64747c2adc4c2d956b221334edd02da80e2117ab950fd6dade7075a36c4c8b159ab2fc03129b7d3b0af5327037d8329068a90ed8dcc65bb1f9f1',
          'name': 'Mr. Basic Meets Bits \'N Bytes (1983) (Mattel) [!]', 'mapper': 0 },
        { 'hash': 'e7344692ba73c4904de8d9040fbfbdc9e7c7e764970818325d80f84c8055875755e8ede809e98e746c2de377130c6d0aeda45ccfc0bb886701256be0d1ea6691',
          'name': 'MTE201 Intellivision Test Cartridge (1978) (Mattel)', 'mapper': 8 },
        { 'hash': '280f9907088d3b38e06df3f36a67f41435fa7c7a6c96a3b7e493b548e0ea0539df7f43026c084a6e448c46e1ef3d7cf2f0562296ecf49dbaaf874dcb9e6e2316',
          'name': 'NASL Soccer (1979) (Mattel)', 'mapper': 0 },
        { 'hash': '72cea9b02a20b222089e3aaaa433dc608d124c244d65af6611d7b7bc3afac8bdcddb867d1aa00209770c1978203168a884a3ab0bcd9a7636cc821961c5f5cbf8',
          'name': 'NBA Basketball (1978) (Mattel)', 'mapper': 0 },
        { 'hash': 'a0664a0bffd89dd18bf3bfb09c5e9d48717abb8d9237979e5c57a8846865fa0b38c2f944ecbaae7a7d5d1f50682257e7a800f3bbe4d3245c965da45ed758e974',
          'name': 'NFL Football (1978) (Mattel)', 'mapper': 0 },
        { 'hash': '272a6ed986d86d611aa1083986a51d579903b209df4004d88fb7a2e32b7ab5e461aba3b0bf86f1fa2e4a8f9432f409a08d8753c270837f649560d860a59283dc',
          'name': 'NHL Hockey (1979) (Mattel)', 'mapper': 0 },
        { 'hash': 'ca74807fa028b8e80b86252252ed005b0811ef97667276ad802e893065ca91d72ea4ca0469f06a7162cee478648556c16488fd37bc7dcbe50be17b613a9f1915',
          'name': 'Night Stalker (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'cc4ec80a73ae5b0ed7fcd977e2e5263e4536f0d7e84d79240b04e71b352dd4c977602da95f94f70dd242b322ac6c5eb8366976d8594243ea8ce5a366fcbcdf71',
          'name': 'Nova Blast (1983) (Imagic) [!]', 'mapper': 0 },
        { 'hash': 'f750c077c716ddfdbe7f8b3ec60f4d7615046cfb3c81dced61336246880aa4ce5901c103267ee780a1f995989f8586f1fa6012fb9e5bdd46be522c916648be91',
          'name': 'Number Jumble (1983) (Mattel) [!]', 'mapper': 0 },
        { 'hash': '7e47622b31e89ff279ef6b172922583c7aa93ceac5e9e51f8f60e1b4bf9d9fe46892e9e36d94df987c0b760344d8fbe09eceec291808cf4239b78e14ac974b21',
          'name': 'Pac-Man (1983) (Atarisoft)', 'mapper': 5 },
        { 'hash': '54bfbe5ba2eb8d7813b062488ad5d03cc18379f52f9aa80a165963f5a26090403cf1361923b44fa5125f6de9c816374f9348eeda81c66560c944e9420b77d2b6',
          'name': 'Pac-Man (1983) (Intv Corp)', 'mapper': 5 },
        { 'hash': 'fbc63c82a44c80ca116542980d69c6c53af8c16145cf344fac8c5771d7189cdd2844d5c4e56b85fe4d0ee0b5679a4b59f4290aa5460afbe57083665af4ff2232',
          'name': 'PBA Bowling (1980) (Mattel)', 'mapper': 0 },
        { 'hash': '7b4c4a3dd4f52450e8933db188b11fa5120c7dda6926d837ddac9fd2a215af1141b3830680bce0a7b487a00d9f31050f7b428a93f3912b1646de0e99c7b37120',
          'name': 'PGA Golf (1979) (Mattel)', 'mapper': 0 },
        { 'hash': '317f0081ba2bab9fdd43768cb6adc52ba55804cc7207f47dfaa1736c4682af54b26127e9a497a05551f0ae62642ad0ed1cb47e87d7623bfbfc9f0c8330ac85bb',
          'name': 'Pinball (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '2c6b01684e9a5929dc55e006d31ac742b70f285831a9b2f4581edbc4a7ca8cf5761105cb9e1582953b86a81a71dcc37c0eaa4c3381e9baa78938687f0e978964',
          'name': 'Pitfall! (1982) (Activision) [!]', 'mapper': 0 },
        { 'hash': 'e688b15d007049ffac470e4790f873594fa658185683584dd38a29fd0b30c72a49c32d8051027157f1ea3d03ce1e8399c22413abad821a24d2ddd7c0a5465e02',
          'name': 'Pole Position (1986) (Intv Corp)', 'mapper': 2 },
        { 'hash': '487076c18d65f27f21387d58788a5fd6e8799c849b7d96e7f3186a96c82ed8e80476bb53da1df252ced1ab32d2917950f7b9b63d5f239ce66173962602c1bcd5',
          'name': 'Pong (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': 'b00182001c38df05c250ffa5240a155de6afce3cb57912b34f52a1130259f3d0294620a3cd9471661e4d417f045c7142129173a6757a94d409a67cc62c165813',
          'name': 'Popeye (1983) (Parker Bros)', 'mapper': 0 },
        { 'hash': 'a4bd3ac312603509f4c0c78b2e580454e3c41ffb335ef1f4390743fe5c9b4444725ec37134d946d627cd80a7fc8b27fba6bd5d2d5cc9f71b35d0cd796774fd56',
          'name': 'Q-bert (1983) (Parker Bros)', 'mapper': 0 },
        { 'hash': '40e4709fbfee97a7b21457587f5ecfbd26caacb48014eee9de0b4838362df204f2865452d0f0c3f922a424f277e87214ae081389a4855c3ce8f2828ee6234157',
          'name': 'Reversi (1984) (Mattel)', 'mapper': 0 },
        { 'hash': '2e49444d1ea0f70b83a06ab572371fe9dfdabf2f9c4759b1423000680851a0945728607adf5588f1d089131005012821dd9a049d52d0dd8040a6fd401c5a061e',
          'name': 'River Raid (1982-83) (Activision) [!]', 'mapper': 0 },
        { 'hash': '3c96f14979d80c420a55c36a8738fa696fbb1d08f216ea3acc50447c65671f83d83fb66d002211d5d84fc2f4a930f9f4257959a4183fa5b29e9ccede6af8e475',
          'name': 'River Raid V1 (Prototype) (1982-83) (Activision)', 'mapper': 0 },
        { 'hash': '9b3e1e597ddf85ecde224bfa429baf5614b975cf2533bd714e7ec4cb9eea1b82e62a44a27832e47d54b4f34c78a23fb32cd8b8009ce77eecc0e7774d2763175e',
          'name': 'Robot Rubble V1 (Prototype) (1983) (Activision)', 'mapper': 0 },
        { 'hash': '5fcc955a84633b6cc404e5de105a7a15a8991e6d929c684f0dfebd5cea0c7d4f1e2a40e16b7ee0398819d3ed6410ca1c7cee0822c473e70aabcb6052f917cb5f',
          'name': 'Robot Rubble V1 (Prototype) (1983) (Activision) [o1]', 'mapper': 0 },
        { 'hash': '01f414a397ba482c5d8568949a57016848ac898e2d39b960da78ffb39f8088a59b3c6032d45f7109612513b2528cabb0af2c4d37852a3612672807d77ee9eccd',
          'name': 'Robot Rubble V2 (Prototype) (1983) (Activision)', 'mapper': 0 },
        { 'hash': 'c8e8da960fd1bc98af4a60aaf30e33edd633b4054afed2a661458da31f1323cb8b2322fa61f9bceb9439fc26dfe64ede9902ae96577026c0535701ad11034c50',
          'name': 'Robot Rubble V3 (Prototype) (1983) (Activision) [!]', 'mapper': 0 },
        { 'hash': 'bbd769eca36b1b334b9059c812bc70675d6922d89949c1ed17b5959a3655b980f270b138a2c9efb2525ed7672f598b632f80aae019ca9915086669b678e6b66f',
          'name': 'Royal Dealer (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '95470bbf13404e90e84b293f45818461dae4ccea3b824e0269e32276101bef66a486a1389cf936aa370dd7b23d43d94c8f16d56e6ac09e7d6a3f8c8b46cfde1c',
          'name': 'Safecracker (1983) (Imagic) [!]', 'mapper': 0 },
        { 'hash': '0dc330665394cdb1cd15b5f14ce5a19ed72be4bfd6f057199e7000be59e158904118888ea32af44c3ff7779ba389029b3831cedb1c8827fa3d697e2162b03e9f',
          'name': 'Santa\'s Helper (1983) (Mattel)', 'mapper': 0 },
        { 'hash': '2d5de786dbeba76ecbd0010440e39f1426bf38410435d704d716f554e67ae3e114ef2f248d797554652eb0a954ebbd9ed0ee81f0a391233ffbfb87a8718f2518',
          'name': 'Scooby Doo\'s Maze Chase (1983) (Mattel)', 'mapper': 0 },
        { 'hash': 'c269b678b6d6a92c2d61de3db3dc50dbb8c95219895e963d9ca6376a9986b2fc7970893c0f0594574e68221adba9d710233c2b3a262835c7faa6be909ccbf93b',
          'name': 'Sea Battle (1980) (Mattel)', 'mapper': 0 },
        { 'hash': 'dd4a1d954c21c771731d39895696f04b3ff8ad1ace855e56fe528e01f3b7a48665b734d86d907b4a4f1b780b765e968e0e513787bbee4dccbcb5a80ff105887d',
          'name': 'Sears Super Video Arcade BIOS (1978) (Sears) [!]', 'mapper': 0 },
        { 'hash': '7557bc9b5ab78bd7484042ef2edf47b9526933e474c1fd9ca021770cf59f5ac9999018981d1702428cff1b92d0cd08a56eb7148a2d29c33978dbb9059c407e9a',
          'name': 'Sewer Sam (1983) (Interphase)', 'mapper': 0 },
        { 'hash': '1b8f1011e804c4e3e323fd35aff6a4ee77412270388e1bba9594d3f892d450b8c36629101d4862368afd38cbb72dba2556846db3697b51a6115cac79390e8a29',
          'name': 'Shark! Shark! (1982) (Mattel) [a1]', 'mapper': 0 },
        { 'hash': 'e76eeb500ac8026409dd933de0a4794fe44d7c0f82c73ae3fd12b445591239718aa76e6651d225378dfae6cecd9d578609330555ad98f4381a8488fa24f355f9',
          'name': 'Sharp Shot (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '018bc1b5cea73381ad9782cde791859b005db5de356ed7dd483489d8d1ee9880c97875ea120c08a7c62e1da7b926f4a2def72355f8e486315474f8b626c68469',
          'name': 'Slam Dunk - Super Pro Basketball (1987) (Intv Corp)', 'mapper': 2 },
        { 'hash': 'e0456562d1d2058527bc0b4713a97d0dc6d81e4e6311f70c49164d7810e9bde7df070860d0ad7508bc99247c7cec4f0b433d2aa1dc7083e9293db046eb402e1c',
          'name': 'Slap Shot - Super Pro Hockey (1987) (Intv Corp)', 'mapper': 0 },
        { 'hash': 'ba754db506ece1ed78d5cefb2984a748923e467978d4eb25cd2f3330c2973111f8801e2f7e1dbc236eaa9a46f9ba2de1a2f6b399c139d76e48d09b4d79472f1b',
          'name': 'Snafu (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '72aabc51ee741503a5302df6d832e1addfd17e370e28c2487c73eb9fbaf667f91299bf7278afee9782c4dc7ac70f61069d314a1aa3461c5c4c206c14f9d3eb6c',
          'name': 'Song Player - Back in the USSR (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': 'bcf1bdce6d24c7e1cbd54630390e3b5fb2c62d56b8117488527c5c522123c74836458a2bd95760e205e215cd53563bab4a75677b27443cd038666d6ca0637fed',
          'name': 'Song Player - Closing Time (2001) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '980d026b44127727ad4bdbf45bdf31079ed0ab810ae74e24e54d9326faa957b6df1754faec417433088bf845b0aabf78f0c33bfee31c43900fea70bd76a8dd2e',
          'name': 'Song Player - Copacabana (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '3e85532e0f6d399107b34e1da3a060b9e6de73b112715b674020f7a121e0f1d4351e11ae8cc4d77b69a3ccc7c68f10540c29bfdce43f2d4aae90d847bf0d8955',
          'name': 'Song Player - Creep (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '9fd708941b96cbad445b666cdf853de084ba7f9d162a676e79247685795e6db650d865d9893a6dea391851cbd8fab20a9419a1573c54e21546b0bf6535f17b48',
          'name': 'Song Player - Nut March (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': 'b57c432d070f524f6f1ecb7771f2034f1ab1fa82daefecc96ca0d537d96187b1b3b634578a3111ff0d26a409a271040b623bb0393232c59b84617944d3684602',
          'name': 'Song Player - Nut Reed (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '195cac2756fd66a50a243f62aded7c5a21faefd0bdd135203a24faa3c77659c4b17128c093590b3a3b6433f065c369208399a077ef82f6f8c88cad87ad7dcaca',
          'name': 'Song Player - Nut Trep (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '9c115969863aaef1f29a7184146bd32091c1db4dca482f3d35e45c5b7a28a677daeb5c31afc744c3331072bb5e4782e4912c88390b45fd994942f94aa09cfa0f',
          'name': 'Song Player - Nut Waltz (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': 'e0fa243bc4a03e1f8daa20be923dc8de08324ef0fc497e906ed4ee438007e7218a7226089b58d4a4d2393d6c5f9ca77b45582aa8592bd3d76748bbdbceacb578',
          'name': 'Song Player - Secret Agent Man (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': 'e24ccaa6989bdb16eb4486f5cc40178d4b324f5b4441274dad5073bab43bc9263798274ce8db6bc9a7a3e86ea26bfa830ec05fd4ecb94bac89600cf67daa40f2',
          'name': 'Song Player - Take on Me 3 (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': 'ed2db40449bc0d60230c30882ca1de61851105f0f7769933d1cca2af3e12ad6d4260b3d5419f525533a38bf3bf7815525efe47109e2a8d0641e61debbe13cf02',
          'name': 'Song Player - Take on Me 6 (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '3ce7e78e3a165ef7dbde7cb4f4ca508557afa31ce8fce6d79fc169ad3a5652e938d9f7d05920ef4535aa075547d3f5db54021f9c0dd525b3a6949d882a8ea770',
          'name': 'Song Player - Too Sexy (1999) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '72e29060da99ab6d8986187626a4f1003727c59eec3ff0e716810635181d26b84574fc19c78bc31a02a432d4151950c0f7273707821771770cd25ad76bc3deb9',
          'name': 'Space Armada (1981) (Mattel)', 'mapper': 0 },
        { 'hash': 'dfb744defa7161290df347f3a902323ba32ccbf31d706fc887b771b9da746c1053efdbd175c8db7c47c730dd7b16414ab01ed8116ecace2013cc1811cfa29574',
          'name': 'Space Battle (1979) (Mattel)', 'mapper': 0 },
        { 'hash': '4c064a265a2a7b766f1b4a6ab29abfcd0b3902f47426f5a76d7e4590d8ab54434d7421cb5405a65d9b21830ad681a98f633147b43e0f712195b5a33025f9c3b9',
          'name': 'Space Cadet (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '02a892c1eac616b4cdda910af515144e4640b491b1e5488893cd5352569f323b639f2fbde646d57c704f1adcea6d39a1f79bfffc62b18fac04004b2f60358aa9',
          'name': 'Space Hawk (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '29fb6a491682a083eea3ec68393c50dfd62454d1a8d5956e5606fabf6840f1f87e2251f27af78e3ac24476d043348c7f0c1a29f61bcc121b19e53a6edc36599d',
          'name': 'Space Spartans (1981) (Mattel) [!]', 'mapper': 0 },
        { 'hash': '0d04c9d9b8d0035d5baf3623bf218f91f9006196a3389ecbd8500e20d01ed0c8fd20b88be78797abf089183b00589689972b21cbca4f3ce23610abfe0ebf4e17',
          'name': 'Spiker! - Super Pro Volleyball (1988) (Intv Corp)', 'mapper': 2 },
        { 'hash': 'd96456217a4ed3de1ef2b8a90d6881dc25397473e85e91a23a72ff8d2f6ce25166c583177ae8ea30ca58864b497ee5e0155081edb4ab21156b220cef495281c8',
          'name': 'Stadium Mud Buggies (1988) (Intv Corp)', 'mapper': 2 },
        { 'hash': '79c73d6281141b56db0395635c0cc65c16f4cdab14ccf5ba5e9c83b89e3447b888dc5fd1730cf87337ec4375438721d6cbac5d5b32ecfbf73033548b8222e926',
          'name': 'Stampede (1982) (Activision) [!]', 'mapper': 0 },
        { 'hash': 'f1e597e967c000010cb1b9c5a377afbc33bd3529192ac54e585dcfa140d53dd87099b7f7e665911400e45da8197e06766a6ec08333d70f2308d34c01fdd10547',
          'name': 'Star Strike (1981) (Mattel)', 'mapper': 0 },
        { 'hash': 'db3791d678658a395154e983aa9a6a24e4b0c0a93708f4e833dadd119243cd5b6d0643203df72f994fc63987f8d2f674ac93af01b19e9f7600761585c41b5a95',
          'name': 'Star Wars - The Empire Strikes Back (1983) (Parker Bros)', 'mapper': 0 },
        { 'hash': '7f292e65f934fe6ee7906a6c4ae4d80b88852ffec6b6885fc6f382f521864df0d580932f42137e3b4771a6cf32f4427dc8692ad66f803ec7afd41247dd1edb71',
          'name': 'Street (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '7e4e3b0ddb3d4d70ec6002bd14f2f7ee5e2dd6eea0a2c13b34c17df56292ce69166c57a4489760eea49314a129dd65c4628f45b64b6c3e12f9c02e86c1db178e',
          'name': 'Sub Hunt (1981) (Mattel)', 'mapper': 0 },
        { 'hash': 'b3682967f29d393a069b4b42169540cca61fa824db747a5ed3b943029573ee7d546f7c5d9709ee80c95316e2439c6669a0fe7e93c7da80e8c7c9b13aa7b73c93',
          'name': 'Super Cobra (1983) (Konami)', 'mapper': 0 },
        { 'hash': '57f0bfdf2cc41a66691199df673f5d6fb53346ce8c4abe789a7189168803b6ea4aeff35abe0bee829a98fe0195d844541d7defda221d5b13a34db4a060fe8d90',
          'name': 'Super Masters! (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'baaa92f0ab7f3eec4562b9f8aaa717d2f3068f81ff70497cf9d780c8f9d58a44f5174296d56695caccc7fb92e11197088e2c498d0f91734be86aae40faefc38c',
          'name': 'Super Pro Decathlon (1988) (Intv Corp)', 'mapper': 2 },
        { 'hash': 'd128c715a9ef6d3ce24b25ec9b1e560c72ec67e0f623e8653403dea17b01f47cde9b0e070f11971a75d31bae444c10b37c67936a426f01d9f1caaf75d42ad416',
          'name': 'Super Pro Football (1986) (Intv Corp)', 'mapper': 2 },
        { 'hash': 'a312ad11421ce389d6ba733f6850db77370915f2b3e03716a5989c3de676f2b8db54c9e9b75d10023623f7a9ea49f4d9d3a32c630f7f473569e7d793d1a9ba08',
          'name': 'Super Soccer (1983) (Mattel)', 'mapper': 0 },
        { 'hash': '4f01be2c88fd4f9d6fd058bd73c6b59cf1b7b9eb3e7764b8b1b3aee44e4bca0d235ab510db8632e7be857e50f1e8baeabb8943dbab75c608ea7e00aed23aa48b',
          'name': 'Swords and Serpents (1982) (Imagic) [!]', 'mapper': 0 },
        { 'hash': 'fb774a06c53b4668dfffbc3a0a41dc5336e308f9aacf332452e71bd34728418fb03e7ba3ae1ae25b8987205b455254c73d49bed886841c7f9ca2e5d94b0b3e53',
          'name': 'Takeover (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'bc97e93357660f17038fa012abff210514b042dc315e52c472180b5aac7e83dc92290ac3a082d59b089fe89e207fe666d220480477e6bf67e46e504c978a5d48',
          'name': 'Tennis (1980) (Mattel)', 'mapper': 0 },
        { 'hash': '2763c87a87abfd01e3d5b8c5209b828acd0e16aca1244b5b6a574e6bf78400985c15f364e2c07da32075a3b67f6e6c594784ce4077f0da574abd8907a82f25d1',
          'name': 'Tetris (2000) (Joseph Zbiciak)', 'mapper': 0 },
        { 'hash': '3021f5b7df3a31fcc75691de9b1be22d42b389bb16b84c07dfe46e671501599af719c27834a4fae533921b04ce1f8cee3dd402242a5b6b4d5c6fc4e64cb2235e',
          'name': 'Thin Ice (Prototype) (1983) (Intv Corp) [!]', 'mapper': 0 },
        { 'hash': '3f44badc5fe506ddc123ad7316e992c74cee1d651cbecad39357f41d3689a09f56952c11ba30c755e7d288fd088c2b1b56c1f23f652e724036c60c515f9533d1',
          'name': 'Thunder Castle (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'b29e38e7b5ddc48e6e88e0da2584d39b739991057550f8725f20b44c1ae067572d7cf74dc93605e504ff359155015ab7641653b78a680e5f1e67b7e7cb2cb058',
          'name': 'Tower of Doom (1986) (Intv Corp)', 'mapper': 3 },
        { 'hash': '6d89ea1e335b1791f67df2f1c9b107d776e1a76c0777be72e030db0036954a991bb819eb045a4b087770548549be34511760bd0f09d33113a04c854bc78b2c57',
          'name': 'Triple Action (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '3fd81c3c78edddbe1b4ae6e94fec7c892056aabc8e67a6086a3424ef74f270eeddbc333e1ac9f58f3259b47e11c67772c182de1e054963643f8c71675da46da4',
          'name': 'Triple Challenge (1986) (Intv Corp)', 'mapper': 9 },
        { 'hash': '23aaa1a7e00aa379d731ff2a4e7ae2b9fd960b630d35e79031d4bf3f374fa5f92f29c7b7d0ad561f228e1c6d39db597db3680ed6c2adf18be45fbc999d514178',
          'name': 'TRON - Deadly Discs (1981) (Mattel)', 'mapper': 0 },
        { 'hash': 'ccdcba1ea9db56a179c961321fadde393ffc1046ce0aabd986a452f20c641b00062b6c8d66b3a991ecb2b19efcbc309bbb51f066454885dee63a08a2edcfad08',
          'name': 'TRON - Deadly Discs - Deadly Dogs (1987) (Intv Corp)', 'mapper': 0 },
        { 'hash': '917c189c983ab74d0d8890bbd6a23b82ead6180e681f1c2edff6db0a185f654a7b306265be0740dc0467083eada2c2f403f988519af195c951d3d62f3bb6d8f7',
          'name': 'TRON - Maze-A-Tron (1981) (Mattel)', 'mapper': 0 },
        { 'hash': 'e4ebdfc618b95a9b061b329f6cc1df27d586bd7fee91e5a23abc3eef68794f93cedc7516de800c27b6f2468b5ee413040541c2f7226dc4f910269704f3a62a3c',
          'name': 'TRON - Solar Sailer (1982) (Mattel)', 'mapper': 0 },
        { 'hash': 'afd31ba4f4a3e052694a7f3e8617d8fd8c73b64ae758aa91795a9deb22cd2f397f7554fab8579b08c29aee25f6b60996e7c27a57af3ab64a4f99dd42d19915d2',
          'name': 'Tropical Trouble (1982) (Imagic) [!]', 'mapper': 0 },
        { 'hash': '12a00f173cde812ad8faeb667811d515bf56ad4d89ecb5ac1b8472b35f7a283f9bf0ad1f91968550c4c1506e57db5744f946c43a378f1811df1033ddd09f75c9',
          'name': 'Truckin\' (1983) (Imagic) [!]', 'mapper': 0 },
        { 'hash': '7fb8b21b1b9c4cd390d3d5a8583cd6db5301e17405923c1b2cc5992be4f6ee383534f53444e6edffe92874bddba859fff89d904482ef45405406dd1ff87e1f33',
          'name': 'Turbo (1983) (Coleco)', 'mapper': 0 },
        { 'hash': '28b5d5b567d5bc558e3bd7c35e14e761005ba6e5bae73d9f4a75139d82303f82cc23a07ec43f6344394badbf483fc05e66388fb05a578126124af2192a07dd2a',
          'name': 'Tutankham (1983) (Parker Bros)', 'mapper': 0 },
        { 'hash': '67684d106a115c83e207cd49aa0ca84ff5fc5bd7c55ce0c14c1d9a36c52c098045f6aeefe76e67a693cadfd54a792f35b8e70c8ade585384948d5ada54ef3f47',
          'name': 'USCF Chess (1981) (Mattel)', 'mapper': 4 },
        { 'hash': 'bb066dd50f1ffaf6cff880ca8a919da4e04572a114196794f9273e7a0928cbaba0201666c007dc39352f570fa9cc834a4b5d6256bafffc2b89c79714eef62c60',
          'name': 'U.S. Ski Team Skiing (1980) (Mattel)', 'mapper': 0 },
        { 'hash': '82d024cc6a9fe8a1a5de6ef00e4e0a2a4bfb63f58c6f8ad41df814c339bbca407688e25b1ca3a6fd04b9c748492c6bf2fc9f80f71790df60ad7c2c0a54f21b80',
          'name': 'Utopia (1981) (Mattel)', 'mapper': 0 },
        { 'hash': '79c970eeda86aba027f538fb9fa746480b09e28fc2009a7e33bd02f458cd0505530a73efecdd64f820a82de969608444be90abc98fe237a5718e42930adcf4c0',
          'name': 'Vectron (1982) (Mattel)', 'mapper': 0 },
        { 'hash': '944cc2e1b2b45f2c670abea3d2e56520c131ff3eda1f0f0a48b5286916c048cd7eb6783256edb52d9ee32801331d9536a6a32a810f7fcd76378a8ad64676f7d6',
          'name': 'Venture (1982) (Coleco)', 'mapper': 0 },
        { 'hash': 'db89399981f0bc7cafe1b51a024da53e4937f3c330db6a33b67f1ff92f69b66ef4255b3e285cf9f6e440c14805e2ebf7ff20ed25a6ace2eb9b551472d4dc9b44',
          'name': 'White Water! (1983) (Imagic) [!]', 'mapper': 0 },
        { 'hash': '43f4325de0e43c44cdf1f1994cc012257c80506c4192d07f2ebebd1d11ffc0882be22c9de08ae321359c1307d5e5dec9523d4508bfabba50ff1e5d7658954251',
          'name': 'World Cup Football (1985) (Nice Ideas)', 'mapper': 0 },
        { 'hash': '01ff29198302efe9d1d235cc2b7200a426c5941ea79820c54a55104834074858505093201b58fbfad8445aa87b6abaeb410ca0e71dce74163ea337cd60e4cc2f',
          'name': 'World Series Major League Baseball (1983) (Mattel) [b1]', 'mapper': 1 },
        { 'hash': '895500908cbd1ad08d286e7450d942b52b4fd09817cd59189ada2132376d363b54f9629791da756c8c5c8ca8d577c2a8ef43fb71e6b9dc4858c5011af1b5a366',
          'name': 'World Series Major League Baseball (1983) (Mattel) [!]', 'mapper': 0 },
        { 'hash': 'b395f2eb61b50137a21dcafb585d0bb732522e721332a1da52a2852ec91933490ef8540fcfc12dc7dbaee1e4c8f7303f6c5143ff5ecbacec8379fa2c5b432602',
          'name': 'Worm Whomper (1983) (Activision) [!]', 'mapper': 0 },
        { 'hash': '2264f3e8b2f80f0e1fd31d7bc81ee6d77c28a6c502e7bdf4e79a49c974f2fca25e7375c877da26b30a6eb29bf6207c5a1dff60a63ae77124f15e6d4643fb5a5e',
          'name': 'Zaxxon (1982) (Coleco)', 'mapper': 0 }
]

mappers = {
    0: {
        '5000':
            { 'offset': 0x0000, 'words': 0x2000, 'loc': 0x5000, 'key': '5000'},
        'd000':
            {'offset': 0x2000, 'words': 0x1000, 'loc': 0xd000, 'key': 'd000'},
        'f000':
            {'offset': 0x3000, 'words': 0x1000, 'loc': 0xf000, 'key': 'f000'}
    },
    1: {
        '5000':
            { 'offset': 0x0000, 'words': 0x2000, 'loc': 0x5000, 'key': '5000'},
        'd000':
            {'offset': 0x2000, 'words': 0x3000, 'loc': 0xd000, 'key': 'd000'},
    },
    2: {
        '5000':
            { 'offset': 0x0000, 'words': 0x2000, 'loc': 0x5000, 'key': '5000'},
        '9000':
            {'offset': 0x2000, 'words': 0x3000, 'loc': 0x9000, 'key': '9000'},
        'd000':
            {'offset': 0x5000, 'words': 0x1000, 'loc': 0xd000, 'key': 'd000'}
    },
    3: {
        '5000':
            {'offset': 0x0000, 'words': 0x2000, 'loc': 0x5000, 'key': '5000'},
        '9000':
            {'offset': 0x2000, 'words': 0x2000, 'loc': 0x9000, 'key': '9000'},
        'd000':
            {'offset': 0x4000, 'words': 0x1000, 'loc': 0xd000, 'key': 'd000'},
        'f000':
            {'offset': 0x5000, 'words': 0x1000, 'loc': 0xf000, 'key': 'f000'}
    },
    4: {
        '5000':
            {'offset': 0x0000, 'words': 0x2000, 'loc': 0x5000, 'key': '5000'},
        'd000':
            {'loc': 0xd000, 'words': 0x0400, 'ram': 8, 'key': 'd000'}
    },
    5: {
        '5000':
            {'offset': 0x0000, 'words': 0x3000, 'loc': 0x5000, 'key': '5000'},
        '9000':
            {'offset': 0x3000, 'words': 0x3000, 'loc': 0x9000, 'key': '9000'}
    },
    6: {
        '6000':
            {'offset': 0x0000, 'words': 0x2000, 'loc': 24576, 'key': '6000'}
    },
    7: {
        '4800':
            {'offset': 0x0000, 'words': 0x2000, 'loc': 18432, 'key': '4800'}
    },
    8: {
        '5000':
            {'offset': 0x0000, 'words': 0x2000, 'loc': 0x5000, 'key': '5000'},
        'd000':
            {'offset': 0x2000, 'words': 0x1000, 'loc': 0xd000, 'key': 'd000'},
        'f000':
            {'offset': 0x3000, 'words': 0x1000, 'loc': 0xf000, 'key': 'f000'}
    },
    9: {
        # defined in spreadsheet but does not actually exist?
    }
}

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
        useparam = 0
        if info.get("ram", -1) != -1:
            usetype = ord('R') # ram page
            useparam = info["ram"]
        else:
            bytes = info["words"] * BYTESPERWORD
            binfile.seek(info["offset"]*BYTESPERWORD)
            if info.get("page", -1) != -1:
                usetype = ord('P') # bankswitched page
                usepage = info["page"]
                useparam = 1 << usepage
            if pagedata.get(usepage, -1) == -1:
                pagedata[usepage] = bytearray(MAXADDR*BYTESPERWORD)
            pagedata[usepage][loc*BYTESPERWORD:loc*BYTESPERWORD+bytes] = binfile.read(bytes)

        for block in range(startblock, endblock + 1):
            blocktype[block] = usetype
            blockdetails[block * 2 + 0] |= useparam >> 8
            blockdetails[block * 2 + 1] |= useparam & 0xff

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
    print(items)
    return items

def parsebin(binname):
    cfgname = str(Path(binname).with_suffix('.cfg'))
    ecsname = str(Path(binname).with_suffix('.ecs'))
    cfginfo = parsecfg(cfgname)
    print(sys.argv[0] + ': converting', binname, "->", ecsname)
    convert(open(binname, 'rb'), cfginfo, open(ecsname, 'wb'))

def sha512hash(filename):
    buffer_size = 0x10000
    sha512 = hashlib.sha512()

    with open(filename, 'rb') as f:
        while True:
            data = f.read(buffer_size)
            if not data:
                break
            sha512.update(data)
    return sha512.hexdigest()

def main():
    args = sys.argv[1:]
    if (len(args) < 1):
        print('Usage:', sys.argv[0], '<filespec>')
        sys.exit(1)
    for name in args:
        if name.lower().endswith(".bin"):
            cfgname = str(Path(name).with_suffix('.cfg'))
            if (os.path.exists(cfgname)):
                print(sys.argv[0] + ':', cfgname, 'exists, overriding LUT')
                parsebin(name)
            else:
                hash = sha512hash(name)
                for cartridge in cart_data:
                    if hash == cartridge['hash']:
                        print(sys.argv[0] + ': matched', cartridge['name'])
                        mapper = mappers[cartridge['mapper']]
                        ecsname = Path(name).with_suffix('.ecs')
                        convert(open(name, 'rb'), mapper, open(ecsname, 'wb'))
                        print(sys.argv[0] + ': converted to', ecsname)
                        break
                else:
                    print(sys.argv[0] + ': unknown hash and no cfg, aborting')
        else:
            print('filespec', name, 'does not end with .bin and will not be processed')
    if getattr(sys, 'frozen', False):
        input("Press enter to proceed...")

if __name__ == '__main__':
    main()
