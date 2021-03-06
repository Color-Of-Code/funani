# -*- coding: utf-8 -*-

import logging
import os
from metadb import MetadataDatabase
from mediadb import MediaDatabase
from address import hash_file, shard

LOGGER = logging.getLogger('funanidb')

EXTENSIONS_IMAGES = (
    '.jpg', '.jpeg', '.png', '.dng',
    '.tif', '.tiff', '.pnm', '.cr2', '.cr3', '.bmp',
    '.xcf', '.gif')
EXTENSIONS_VIDEO = ('.mts', '.mp4', '.mov', '.avi', '.mpg', '.3gp', '.wmv')
EXTENSIONS_AUDIO = ('.aac', '.m4a', '.mp3', '.opus')
EXTENSIONS_ALL = EXTENSIONS_IMAGES + EXTENSIONS_VIDEO + EXTENSIONS_AUDIO

# Files to fully ignore during processing
IGNORE_FILES = ('thumbs.db', '.nomedia')

class FunaniDatabase(object):

    ROOT_PATH = ''
    metadata_db = None
    media_db = None

    def __init__(self, section):
        self.ROOT_PATH = section['path']
        self.metadata_db = MetadataDatabase(self.ROOT_PATH)
        self.media_db = MediaDatabase(self.ROOT_PATH, section['auto-reflink'])
        LOGGER.debug("Initialized database at '%s'", self.ROOT_PATH)

    def __str__(self):
        return 'FUNANIDB:{}'.format(self.ROOT_PATH)

    def verify_files(self, force, metadata):
        if metadata:
            # TODO: check the metadata & SQL DB
            pass
        else:
            # verify the files (data scrubbing)
            self.media_db.verify_files(force)

    def meta_get(self, hash_values, fixdb):
        for hash_value in hash_values:
            self.metadata_db.dump(hash_value)
        # TODO: check & upload the metadata in the SQL db too

    def check_file(self, file_path):
        srcfullpath = os.path.abspath(file_path)
        srcfullpath = os.path.realpath(srcfullpath)

        #TODO: Build a DB table with "root paths" (repetitive Media base paths)
        #      root paths shall not be substrings of each other
        #TODO: Build a DB table with "root path id" | modtime | "relative path" | hash

        #TODO: Try to find the modtime:/filepath in the DB -> if yes return that metadata

        # otherwise fall back to this default behaviour
        hash_value = hash_file(srcfullpath)
        print("hash:", hash_value)
        self.metadata_db.dump(hash_value)

    def _traverse(self, directory_path, reflink):
        LOGGER.info("recursing through '%s'", directory_path)
        for root, dirs, files in os.walk(directory_path):
            for name in files:
                if name.lower().endswith(EXTENSIONS_ALL):
                    srcfullpath = os.path.join(root, name)
                    self.import_file(srcfullpath, reflink)
                else:
                    if name.lower() not in (IGNORE_FILES): # avoid noise in output
                        LOGGER.warning("skipping '%s'", os.path.join(root, name))

    def import_recursive(self, src, reflink):
        """Import media from a src directory recusively.

        Args:
            src (str): The path to import
            reflink (bool): Use reflinks if the backend FS supports it

        Returns:
            Nothing.
        """
        srcfullpath = os.path.abspath(src)
        srcfullpath = os.path.realpath(src)
        if os.path.isfile(srcfullpath):
            self.import_file(srcfullpath, reflink)
        else:
            self._traverse(srcfullpath, reflink)

    def import_file(self, src, reflink):
        """Import a single media file.

        Args:
            src (str): The path to the file to import
            reflink (bool): Use a reflink if the backend FS supports it

        Returns:
            Nothing.
        """
        srcfullpath = os.path.abspath(src)
        srcfullpath = os.path.realpath(srcfullpath)

        # compute the hash and relative path to the file
        hash_value = hash_file(srcfullpath)
        reldirname = shard(hash_value, 2, 2)

        (dstfullpath, is_duplicate) = self.media_db.import_file(srcfullpath, reldirname, reflink)
        self.metadata_db.import_file(srcfullpath, dstfullpath, reldirname)

