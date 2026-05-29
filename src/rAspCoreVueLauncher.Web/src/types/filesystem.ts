export interface FileEntry {
  name: string
  path: string
  isDirectory: boolean
  size: number
  modified: string
  attributes: number
}

export interface DirectoryListing {
  path: string
  parent: string | null
  entries: FileEntry[]
}

export interface WriteFileRequest {
  path: string
  content: string
  overwrite: boolean
}

export interface MkdirRequest {
  path: string
}

export interface MoveRequest {
  source: string
  destination: string
  overwrite: boolean
}

export interface CopyRequest {
  source: string
  destination: string
  overwrite: boolean
}

export interface DeleteRequest {
  path: string
  permanent: boolean
}
