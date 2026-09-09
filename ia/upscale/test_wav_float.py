import os
import struct
import tempfile
import unittest
import wave
import numpy as np
import util_audio as audio


def riff(fmt, data, extra=b''):
    chunks = b'fmt '+struct.pack('<I',len(fmt))+fmt+extra+b'data'+struct.pack('<I',len(data))+data
    if len(data)&1: chunks += b'\0'
    return b'RIFF'+struct.pack('<I',len(chunks)+4)+b'WAVE'+chunks


class WavFloatTests(unittest.TestCase):
    def test_exportacion_float_exacta_y_no_trunca_invalido(self):
        with tempfile.TemporaryDirectory() as directory:
            path=os.path.join(directory,'cache.wav')
            x=np.random.default_rng(41).uniform(-1.5,1.5,(70001,2))
            before=x.copy()
            audio.escribir_wav_float(path,x)
            y,fs=audio.leer_wav(path)
            self.assertEqual(fs,44100)
            np.testing.assert_array_equal(y,x.astype(np.float32).astype(np.float64))
            np.testing.assert_array_equal(before,x)
            with open(path,'rb') as f: saved=f.read()
            for invalid in (np.nan,np.inf,1e40):
                with self.assertRaises(ValueError): audio.escribir_wav_float(path,np.full((2,2),invalid))
                with open(path,'rb') as f: self.assertEqual(f.read(),saved)
            audio.escribir_wav_float(path,np.zeros((0,2)))
            self.assertEqual(audio.leer_wav(path)[0].shape,(0,2))

    def read(self, data):
        with tempfile.TemporaryDirectory() as directory:
            path=os.path.join(directory,'input.wav')
            with open(path,'wb') as f: f.write(data)
            return audio.leer_wav(path)

    def test_float_extensible_picos_y_precision(self):
        x=np.array([0,1.25,-1.4,1e-8,-1e-8,.999999],dtype='<f4')
        for ext in (False,True):
            fmt=struct.pack('<HHIIHH',0xfffe if ext else 3,2,44100,44100*8,8,32)
            if ext: fmt+=struct.pack('<HHI',22,32,3)+bytes.fromhex('0300000000001000800000aa00389b71')
            out,rate=self.read(riff(fmt,x.tobytes(),b'JUNK\x03\0\0\0abc\0'))
            self.assertEqual(rate,44100)
            np.testing.assert_array_equal(out,x.astype(np.float64).reshape(-1,2))

    def test_pcm_y_bloques(self):
        for bits in (16,24,32):
            values=np.array([-(1<<(bits-1)),-1,0,1,(1<<(bits-1))-1],dtype=np.int64)
            values=np.tile(values,14000)
            if bits==24:
                raw=np.stack((values&255,(values>>8)&255,(values>>16)&255),axis=1).astype(np.uint8).tobytes()
            else: raw=values.astype('<i2' if bits==16 else '<i4').tobytes()
            fmt=struct.pack('<HHIIHH',1,1,48000,48000*bits//8,bits//8,bits)
            out,_=self.read(riff(fmt,raw))
            np.testing.assert_array_equal(out[:,0],values.astype(np.float64)/(1<<(bits-1)))

    def test_invalidos_y_vacio(self):
        fmt=struct.pack('<HHIIHH',3,2,44100,352800,8,32)
        self.assertEqual(self.read(riff(fmt,b''))[0].shape,(0,2))
        good=riff(fmt,np.zeros(6,dtype='<f4').tobytes())
        for n in range(len(good)):
            with self.assertRaises(ValueError): self.read(good[:n])
        for invalid in (np.nan,np.inf,-np.inf):
            with self.assertRaises(ValueError): self.read(riff(fmt,np.full(2,invalid,dtype='<f4').tobytes()))
        with self.assertRaises(ValueError): self.read(riff(fmt,b'abc'))


if __name__=='__main__': unittest.main()
