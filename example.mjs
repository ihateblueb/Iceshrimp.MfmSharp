/// <reference types="./Iceshrimp.MfmSharp/bin/Release/net9.0/Iceshrimp.MfmSharp.d.ts" />
import * as mfm from "./Iceshrimp.MfmSharp/bin/Release/net9.0/Iceshrimp.MfmSharp.mjs";

const inputText =
    `<center>
Hello $[tada everynyan! 🎉]

I'm @ai, A bot of misskey!

https://github.com/syuilo/ai
</center>`;

// Generate a MFM tree from the full MFM text.
const mfmTree = mfm.parse(inputText);
console.log(mfmTree)

// Generate a MFM tree from the simple MFM text.
const simpleMfmTree = mfm.parseSimple('I like the hot soup :soup:');
console.log(simpleMfmTree)

// Reverse to a MFM text from the MFM tree.
const text = mfm.toString(mfmTree);
console.log(text)