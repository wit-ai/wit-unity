/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi.TTS.Data
{
    /// <summary>
    /// An enum used for the various types of lip and mouth positions possible during speech.
    /// A single viseme could be used to represent one or more phonemes.
    /// More Information: https://visagetechnologies.com/uploads/2012/08/MPEG-4FBAOverview.pdf
    /// </summary>
    public enum Viseme
    {
        /// <summary>
        /// Phonemes: (...)
        /// Ex: mouth is closed
        /// </summary>
        sil,

        /// <summary>
        /// Phonemes: p, b, m
        /// Ex: put, but, mouse
        /// </summary>
        PP,

        /// <summary>
        /// Phonemes: f, v
        /// Ex: fine, verb
        /// </summary>
        FF,

        /// <summary>
        /// Phonemes: th, dh
        /// Ex: three, the
        /// </summary>
        TH,

        /// <summary>
        /// Phonemes: t, d
        /// Ex: truck, duck
        /// </summary>
        DD,

        /// <summary>
        /// Phonemes: k, g
        /// Ex: kit, get, thing
        /// </summary>
        kk,

        /// <summary>
        /// Phonemes: SH, ZH, CH, JH
        /// Ex: shift, treasure, check, jungle
        /// </summary>
        CH,

        /// <summary>
        /// Phonemes: s, z
        /// Ex: sit, zebra
        /// </summary>
        SS,

        /// <summary>
        /// Phonemes: n, l
        /// Ex: no, long
        /// </summary>
        nn,

        /// <summary>
        /// Phonemes: R, ER, AXR
        /// Ex: right, her, water
        /// </summary>
        RR,

        /// <summary>
        /// Phonemes: AA, AH, AX, A(Y), A(W)
        /// Ex: car, cut, about
        /// </summary>
        aa,

        /// <summary>
        /// Phonemes: EH, AE, E(Y)
        /// Ex: bay, bed, cat
        /// </summary>
        E,

        /// <summary>
        /// Phonemes: IH, IY, IX, Y
        /// Ex: hit, here
        /// </summary>
        ih,

        /// <summary>
        /// Phonemes: AO
        /// Ex: talk, toe
        /// </summary>
        oh,

        /// <summary>
        /// Phonemes: UW, UH, W
        /// Ex: boot, book, how, water
        /// </summary>
        ou
    }
}
